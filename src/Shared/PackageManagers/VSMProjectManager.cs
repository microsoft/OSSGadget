// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Helpers;
    using Microsoft.Extensions.Caching.Memory;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class VSMProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "vsm";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_VS_MARKETPLACE_ENDPOINT { get; set; } = "https://marketplace.visualstudio.com";

        public VSMProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public VSMProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one VS Marketplace package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> the path or file written. </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = $"{purl?.Namespace}.{purl?.Name}";
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(purl?.Namespace) || string.IsNullOrWhiteSpace(purl?.Name) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1} {2}]. All must be defined.", purl?.Namespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                Stream? resultStream = null;
                string? cacheResult = GetCache(packageName);
                if (cacheResult != null)
                {
                    Logger.Debug("Located result for {0} in cache.", packageName);
                    resultStream = new MemoryStream(Encoding.UTF8.GetBytes(cacheResult));
                }
                else
                {
                    using HttpRequestMessage requestMessage = new(HttpMethod.Post, $"{ENV_VS_MARKETPLACE_ENDPOINT}/_apis/public/gallery/extensionquery");
                    requestMessage.Headers.Add("Accept", "application/json;api-version=3.0-preview.1");
                    string? postContent = $"{{filters:[{{criteria:[{{filterType:7,value:\"{packageName}\"}}],pageSize:1000,pageNumber:1,sortBy:0}}],flags:131}}";
                    requestMessage.Content = new StringContent(postContent, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                    resultStream = await response.Content.ReadAsStreamAsync();

                    using StreamReader resultStreamReader = new(resultStream, leaveOpen: true);
                    SetCache(packageName, resultStreamReader.ReadToEnd());
                    resultStream.Seek(0, SeekOrigin.Begin);
                }
                JsonDocument doc = JsonDocument.Parse(resultStream);
                await resultStream.DisposeAsync();

                if (!doc.RootElement.TryGetProperty("results", out JsonElement results))
                {
                    return downloadedPaths;
                }

                /*
                 * This is incredibly verbose. If C# gets a `jq`-like library, we should switch to that.
                 */
                foreach (JsonElement result in results.EnumerateArray())
                {
                    if (!result.TryGetProperty("extensions", out JsonElement extensions))
                    {
                        continue;
                    }

                    foreach (JsonElement extension in extensions.EnumerateArray())
                    {
                        if (!extension.TryGetProperty("versions", out JsonElement versions))
                        {
                            continue;
                        }

                        foreach (JsonElement version in versions.EnumerateArray())
                        {
                            if (!version.TryGetProperty("version", out JsonElement versionString))
                            {
                                continue;
                            }
                            if (versionString.GetString() is string v && !v.Equals(packageVersion))
                            {
                                continue;
                            }

                            // Now we have the right version
                            if (!version.TryGetProperty("files", out JsonElement files))
                            {
                                continue;  // No files present
                            }

                            foreach (JsonElement file in files.EnumerateArray())
                            {
                                // Must have both an asset type and a source
                                if (!file.TryGetProperty("source", out JsonElement source))
                                {
                                    continue;
                                }
                                if (!file.TryGetProperty("assetType", out JsonElement assetType))
                                {
                                    continue;
                                }

                                try
                                {
                                    HttpResponseMessage downloadResult = await httpClient.GetAsync(source.GetString());
                                    downloadResult.EnsureSuccessStatusCode();
                                    string? targetName = $"vsm-{packageName}@{packageVersion}-{assetType}";
                                    string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                                    if (doExtract && Directory.Exists(extractionPath) && cached == true)
                                    {
                                        downloadedPaths.Add(extractionPath);
                                        return downloadedPaths;
                                    }
                                    if (doExtract)
                                    {
                                        downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await downloadResult.Content.ReadAsStreamAsync(), cached));
                                    }
                                    else
                                    {
                                        extractionPath += Path.GetExtension(source.GetString()) ?? "";
                                        await File.WriteAllBytesAsync(extractionPath, await downloadResult.Content.ReadAsByteArrayAsync());
                                        downloadedPaths.Add(extractionPath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Debug(ex, "Error downloading {0}: {1}", source.GetString(), ex.Message);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading VS Marketplace package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());

            List<string> versionList = new();
            if (purl == null || purl.Namespace == null || purl.Name == null)
            {
                return versionList;
            }

            string packageName = $"{purl.Namespace}.{purl.Name}";

            // Double quotes probably aren't allowed in package names, but nevertheless...
            packageName = packageName.Replace("\"", "\\\"");

            try
            {
                Stream? resultStream = null;
                string? cacheResult = GetCache(packageName);
                if (cacheResult != null)
                {
                    Logger.Debug("Located result for {0} in cache.", packageName);
                    resultStream = new MemoryStream(Encoding.UTF8.GetBytes(cacheResult));
                }
                else
                {
                    using HttpRequestMessage? requestMessage = new(HttpMethod.Post, $"{ENV_VS_MARKETPLACE_ENDPOINT}/_apis/public/gallery/extensionquery");
                    requestMessage.Headers.Add("Accept", "application/json;api-version=3.0-preview.1");
                    string postContent = $"{{filters:[{{criteria:[{{filterType:7,value:\"{packageName}\"}}],pageSize:1000,pageNumber:1,sortBy:0}}],flags:131}}";
                    requestMessage.Content = new StringContent(postContent, Encoding.UTF8, "application/json");
                    HttpClient httpClient = CreateHttpClient();

                    HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                    resultStream = await response.Content.ReadAsStreamAsync();
                    using StreamReader resultStreamReader = new(resultStream, leaveOpen: true);
                    SetCache(packageName, resultStreamReader.ReadToEnd());
                    resultStream.Seek(0, SeekOrigin.Begin);
                }

                JsonDocument doc = await JsonDocument.ParseAsync(resultStream);
                await resultStream.DisposeAsync();

                if (!doc.RootElement.TryGetProperty("results", out JsonElement results))
                {
                    return versionList;
                }

                /*
                 * This is incredibly verbose. If C# every gets a `jq`-like library, we should switch to that.
                 */
                foreach (JsonElement result in results.EnumerateArray())
                {
                    if (!result.TryGetProperty("extensions", out JsonElement extensions))
                    {
                        continue;
                    }

                    foreach (JsonElement extension in extensions.EnumerateArray())
                    {
                        if (!extension.TryGetProperty("versions", out JsonElement versions))
                        {
                            continue;
                        }

                        foreach (JsonElement version in versions.EnumerateArray())
                        {
                            if (!version.TryGetProperty("version", out JsonElement versionString))
                            {
                                continue;
                            }
                            Logger.Debug("Identified {0} version {1}.", packageName, versionString.GetString());
                            versionList.Add(versionString.GetString() ?? string.Empty);
                        }
                    }
                }

                return SortVersions(versionList.Distinct());
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Debug("Unable to enumerate versions (404): {0}", ex.Message);
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error enumerating VS Marketplace packages: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                HttpClient httpClient = CreateHttpClient();

                return await GetHttpStringCache(httpClient, $"{ENV_VS_MARKETPLACE_ENDPOINT}/items?itemName={purl.Namespace}/{purl.Name}", useCache);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching VS Marketplace metadata: {0}", ex.Message);
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            // there is no version page for marketplace vsix
            return new Uri($"{ENV_VS_MARKETPLACE_ENDPOINT}/items/itemName={purl?.Name}");
        }

        private static string? GetCache(string key)
        {
            string? result = null;
            lock (DataCache)
            {
                result = DataCache.Get<string>($"vsm__{key}");
            }
            return result;
        }

        private static void SetCache(string key, string value)
        {
            lock (DataCache)
            {
                MemoryCacheEntryOptions mce = new() { Size = value.Length };
                DataCache.Set<string>($"vsm__{key}", value, mce);
            }
        }
    }
}