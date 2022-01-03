﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Utilities;

    internal class ComposerProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_COMPOSER_ENDPOINT = "https://repo.packagist.org";

        public ComposerProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public ComposerProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one Composer (PHP) package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageNamespace = purl?.Namespace;
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName) ||
                string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1} {2}]. All three must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                System.Text.Json.JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_COMPOSER_ENDPOINT}/p/{packageNamespace}/{packageName}.json");
                foreach (System.Text.Json.JsonProperty topObject in doc.RootElement.GetProperty("packages").EnumerateObject())
                {
                    foreach (System.Text.Json.JsonProperty versionObject in topObject.Value.EnumerateObject())
                    {
                        if (versionObject.Name != packageVersion)
                        {
                            continue;
                        }
                        string? url = versionObject.Value.GetProperty("dist").GetProperty("url").GetString();
                        System.Net.Http.HttpResponseMessage? result = await httpClient.GetAsync(url);
                        result.EnsureSuccessStatusCode();
                        Logger.Debug("Downloading {0}...", purl);

                        string fsNamespace = OssUtilities.NormalizeStringForFileSystem(packageNamespace);
                        string fsName = OssUtilities.NormalizeStringForFileSystem(packageName);
                        string fsVersion = OssUtilities.NormalizeStringForFileSystem(packageVersion);

                        string targetName = $"composer-{fsNamespace}-{fsName}@{fsVersion}";
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
                            targetName += ".zip";
                            await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                            downloadedPaths.Add(targetName);
                        }
                    }
                }
                if (downloadedPaths.Count == 0)
                {
                    Logger.Debug("Unable to find version {0} to download.", packageVersion);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading Composer package: {0}", ex.Message);
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
            HttpClient httpClient = CreateHttpClient();

            return await CheckJsonCacheForPackage(httpClient, $"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json", useCache);
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            List<string> versionList = new();

            if (string.IsNullOrWhiteSpace(purl?.Namespace) || string.IsNullOrWhiteSpace(purl?.Name))
            {
                return versionList;
            }

            string packageName = $"{purl?.Namespace}/{purl?.Name}";

            try
            {
                HttpClient httpClient = CreateHttpClient();

                System.Text.Json.JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json");

                foreach (System.Text.Json.JsonProperty topObject in doc.RootElement.GetProperty("packages").EnumerateObject())
                {
                    foreach (System.Text.Json.JsonProperty versionObject in topObject.Value.EnumerateObject())
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, versionObject.Name);
                        versionList.Add(versionObject.Name);
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
            try
            {
                string packageName = $"{purl.Namespace}/{purl.Name}";
                HttpClient httpClient = CreateHttpClient();

                return await GetHttpStringCache(httpClient, $"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json");
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching Composer metadata: {0}", ex.Message);
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_COMPOSER_ENDPOINT}/packages/{purl?.Namespace}/{purl?.Name}");
            // TODO: Add version support
        }
    }
}