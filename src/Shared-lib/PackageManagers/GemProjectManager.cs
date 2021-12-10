// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Lib.PackageManagers
{
    using Lib;
    using Lib.PackageManagers;
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
        public static string ENV_RUBYGEMS_ENDPOINT = "https://rubygems.org";
        public static string ENV_RUBYGEMS_ENDPOINT_API = "https://api.rubygems.org";

        public GemProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one RubyGems package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
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
                using HttpClient httpClient = this.CreateHttpClient();

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
                    downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync(), cached));
                }
                else
                {
                    targetName += Path.GetExtension(url) ?? "";
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(targetName);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading RubyGems package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <summary>
        /// Check if the package exists in the respository.
        /// </summary>
        /// <param name="purl">The PackageURL to check.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <returns>True if the package is confirmed to exist in the repository. False otherwise.</returns>
        public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            using HttpClient httpClient = this.CreateHttpClient();

            return await CheckJsonCacheForPackage(httpClient, $"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/versions/{packageName}.json", useCache);
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string packageName = purl.Name;
                using HttpClient httpClient = this.CreateHttpClient();

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
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            if (purl is null || purl.Name is null)
            {
                return null;
            }
            try
            {
                using HttpClient httpClient = this.CreateHttpClient();

                string packageName = purl.Name;
                string contentVersion = await GetHttpStringCache(httpClient, $"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/versions/{packageName}.json") ?? "";
                string contentGem = await GetHttpStringCache(httpClient, $"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/gems/{packageName}.json") ?? "";
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