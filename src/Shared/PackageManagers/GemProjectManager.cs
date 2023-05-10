// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Helpers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class GemProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "gem";

        public override string ManagerType => Type;

        public string ENV_RUBYGEMS_ENDPOINT { get; set; } = "https://rubygems.org";
        public string ENV_RUBYGEMS_ENDPOINT_API { get; set; } = "https://api.rubygems.org";

        public GemProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public GemProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one RubyGems package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                string url = $"{ENV_RUBYGEMS_ENDPOINT}/downloads/{packageName}-{packageVersion}.gem";
                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                string targetName = $"rubygems-{packageName}@{packageVersion}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                if (doExtract && Directory.Exists(extractionPath) && cached == true)
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }
                if (doExtract)
                {
                    downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsStreamAsync(), cached));
                }
                else
                {
                    extractionPath += Path.GetExtension(url) ?? "";
                    await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(extractionPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading RubyGems package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            HttpClient httpClient = CreateHttpClient();

            return await CheckJsonCacheForPackage(httpClient, $"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/versions/{packageName}.json", useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();

                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/versions/{packageName}.json");
                List<string> versionList = new();
                foreach (JsonElement gemObject in doc.RootElement.EnumerateArray())
                {
                    if (gemObject.TryGetProperty("number", out JsonElement version))
                    {
                        string? vString = version.ToString();
                        if (!string.IsNullOrWhiteSpace(vString))
                        {
                            // RubyGems is mostly-semver-compliant
                            vString = Regex.Replace(vString, @"(\d)pre", @"$1-pre");
                            Logger.Debug("Identified {0} version {1}.", packageName, vString);
                            versionList.Add(vString);
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
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            if (purl is null || purl.Name is null)
            {
                return null;
            }
            try
            {
                HttpClient httpClient = CreateHttpClient();

                string packageName = purl.Name;
                string contentVersion = await GetHttpStringCache(httpClient, $"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/versions/{packageName}.json", useCache) ?? "";
                string contentGem = await GetHttpStringCache(httpClient, $"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/gems/{packageName}.json", useCache) ?? "";
                return contentVersion + contentGem;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching RubyGems metadata: {0}", ex.Message);
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_RUBYGEMS_ENDPOINT}/gems/{purl?.Name}");
        }
    }
}