// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.CST.OpenSource.Shared
{
    class VSMProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_VS_MARKETPLACE_ENDPOINT = "https://marketplace.visualstudio.com";

        /// <summary>
        /// Download one VS Marketplace package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>the path or file written.</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract = true, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = $"{purl?.Namespace}.{purl?.Name}";
            var packageVersion = purl?.Version;
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(purl?.Namespace) || string.IsNullOrWhiteSpace(purl?.Name) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1} {2}]. All must be defined.", purl?.Namespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                Stream resultStream = null;
                var cacheResult = GetCache(packageName);
                if (cacheResult != default)
                {
                    Logger.Debug("Located result for {0} in cache.", packageName);
                    resultStream = new MemoryStream(Encoding.UTF8.GetBytes(cacheResult));
                }
                else
                {
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{ENV_VS_MARKETPLACE_ENDPOINT}/_apis/public/gallery/extensionquery");
                    requestMessage.Headers.Add("Accept", "application/json;api-version=3.0-preview.1");
                    var postContent = $"{{filters:[{{criteria:[{{filterType:7,value:\"{packageName}\"}}],pageSize:1000,pageNumber:1,sortBy:0}}],flags:131}}";
                    requestMessage.Content = new StringContent(postContent, Encoding.UTF8, "application/json");
                    var response = await WebClient.SendAsync(requestMessage);
                    resultStream = await response.Content.ReadAsStreamAsync();

                    using var resultStreamReader = new StreamReader(resultStream);
                    SetCache(packageName, resultStreamReader.ReadToEnd());
                    resultStream.Seek(0, SeekOrigin.Begin);
                }
                var doc = JsonDocument.Parse(resultStream);
                await resultStream.DisposeAsync();

                if (!doc.RootElement.TryGetProperty("results", out JsonElement results))
                {
                    return null;
                }

                /*
                 * This is incredibly verbose. If C# gets a `jq`-like library, we should switch to that.
                 */
                foreach (var result in results.EnumerateArray())
                {
                    if (!result.TryGetProperty("extensions", out JsonElement extensions))
                    {
                        continue;
                    }
                    
                    foreach (var extension in extensions.EnumerateArray())
                    {
                        if (!extension.TryGetProperty("versions", out JsonElement versions))
                        {
                            continue;
                        }

                        foreach (var version in versions.EnumerateArray())
                        {
                            if (!version.TryGetProperty("version", out JsonElement versionString))
                            {
                                continue;
                            }
                            if (!versionString.GetString().Equals(packageVersion))
                            {
                                continue;
                            }

                            // Now we have the right version
                            if (!version.TryGetProperty("files", out JsonElement files))
                            {
                                continue;  // No files present
                            }

                            foreach (var file in files.EnumerateArray())
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
                                    var downloadResult = await WebClient.GetAsync(source.GetString());
                                    downloadResult.EnsureSuccessStatusCode();
                                    var targetName = $"vsm-{packageName}@{packageVersion}-{assetType}";
                                    string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                                    if (doExtract && Directory.Exists(extractionPath) && cached == true)
                                    {
                                        downloadedPaths.Add(extractionPath);
                                        return downloadedPaths;
                                    }
                                    if (doExtract)
                                    {
                                        downloadedPaths.Add(await ExtractArchive(targetName, await downloadResult.Content.ReadAsByteArrayAsync(), cached));
                                    }
                                    else
                                    {
                                        targetName += Path.GetExtension(source.GetString()) ?? "";
                                        await File.WriteAllBytesAsync(targetName, await downloadResult.Content.ReadAsByteArrayAsync());
                                        downloadedPaths.Add(targetName);
                                    }
                                }
                                catch(Exception ex)
                                {
                                    Logger.Warn(ex, "Error downloading {0}: {1}", source.GetString(), ex.Message);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error downloading VS Marketplace package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        private static void SetCache(string key, string value)
        {
            lock (DataCache)
            {
                var mce = new MemoryCacheEntryOptions() { Size = value.Length };
                DataCache.Set<string>($"vsm__{key}", value, mce);
            }
        }
        private static string GetCache(string key)
        {
            string result = null;
            lock(DataCache)
            {
                result = DataCache.Get<string>($"vsm__{key}");
            }
            return result;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            
            var versionList = new List<string>(); 
            if (purl == default || purl.Namespace == default || purl.Name == default)
            {
                return versionList;
            }

            var packageName = $"{purl.Namespace}.{purl.Name}";

            // Double quotes probably aren't allowed in package names, but nevertheless...
            packageName = packageName.Replace("\"", "\\\"");

            try
            {
                Stream resultStream = null;
                var cacheResult = GetCache(packageName);
                if (cacheResult != default)
                {
                    Logger.Debug("Located result for {0} in cache.", packageName);
                    resultStream = new MemoryStream(Encoding.UTF8.GetBytes(cacheResult));
                }
                else
                { 
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{ENV_VS_MARKETPLACE_ENDPOINT}/_apis/public/gallery/extensionquery");
                    requestMessage.Headers.Add("Accept", "application/json;api-version=3.0-preview.1");
                    var postContent = $"{{filters:[{{criteria:[{{filterType:7,value:\"{packageName}\"}}],pageSize:1000,pageNumber:1,sortBy:0}}],flags:131}}";
                    requestMessage.Content = new StringContent(postContent, Encoding.UTF8, "application/json");
                    var response = await WebClient.SendAsync(requestMessage);
                    resultStream = await response.Content.ReadAsStreamAsync();
                    using var resultStreamReader = new StreamReader(resultStream, leaveOpen: true);
                    SetCache(packageName, resultStreamReader.ReadToEnd());
                    resultStream.Seek(0, SeekOrigin.Begin);
                }
                var doc = await JsonDocument.ParseAsync(resultStream);
                await resultStream.DisposeAsync();

                if (!doc.RootElement.TryGetProperty("results", out JsonElement results))
                {
                    return versionList;
                }

                /*
                 * This is incredibly verbose. If C# every gets a `jq`-like library, we should switch to that.
                 */
                foreach (var result in results.EnumerateArray())
                {
                    if (!result.TryGetProperty("extensions", out JsonElement extensions))
                    {
                        continue;
                    }

                    foreach (var extension in extensions.EnumerateArray())
                    {
                        if (!extension.TryGetProperty("versions", out JsonElement versions))
                        {
                            continue;
                        }

                        foreach (var version in versions.EnumerateArray())
                        {
                            if (!version.TryGetProperty("version", out JsonElement versionString))
                            {
                                continue;
                            }
                            Logger.Debug("Identified {0} version {1}.", packageName, versionString.GetString());
                            versionList.Add(versionString.GetString());
                        }
                    }
                }

                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error enumerating VS Marketplace packages: {0}", ex.Message);
                return Array.Empty<string>();
            }
        }

        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                return await GetHttpStringCache($"{ENV_VS_MARKETPLACE_ENDPOINT}/items?itemName={purl.Namespace}/{purl.Name}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error fetching VS Marketplace metadata: {0}", ex.Message);
                return null;
            }
        }
    }
}
