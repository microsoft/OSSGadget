﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Helpers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class GolangProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "golang";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_GO_PROXY_ENDPOINT = "https://proxy.golang.org";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_GO_PKG_ENDPOINT = "https://pkg.go.dev";

        public GolangProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public GolangProjectManager(string destinationDirectory) : base(destinationDirectory)
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

            string? packageNamespace = purl?.Namespace;
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1} {2}]. All three must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                string url = $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespace.ToLowerInvariant()}/{packageName.ToLowerInvariant()}/@v/{packageVersion}.zip";
                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                string targetName = $"golang-{packageNamespace}-{packageName}@{packageVersion}";
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
                Logger.Debug(ex, "Error downloading Go package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name) || string.IsNullOrEmpty(purl.Namespace))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageNamespaceLower = purl.Namespace.ToLowerInvariant();
            string packageNameLower = purl.Name.ToLowerInvariant();
            HttpClient httpClient = CreateHttpClient();

            return await CheckHttpCacheForPackage(httpClient, $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}/@v/list", useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            try
            {
                string? packageNamespaceLower = purl?.Namespace?.ToLowerInvariant();
                string? packageNameLower = purl?.Name?.ToLowerInvariant();
                List<string> versionList = new();
                HttpClient httpClient = CreateHttpClient();

                string? doc = await GetHttpStringCache(httpClient, $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}/@v/list");
                if (doc != null)
                {
                    foreach (string line in doc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string lineTrim = line.Trim();
                        if (!string.IsNullOrEmpty(lineTrim))
                        {
                            Logger.Debug("Identified {0}/{1} version {2}.", purl?.Namespace, purl?.Name, lineTrim);
                            versionList.Add(line);
                        }
                    }
                }
                else
                {
                    throw new Exception("Invalid response from Go Proxy.");
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
        {
            if (purl is null || purl.Name is null || purl.Namespace is null)
            {
                return null;
            }
            try
            {
                IEnumerable<string> versions = await EnumerateVersions(purl, useCache);
                if (versions.Any())
                {
                    string latestVersion = versions.Last();
                    string packageNamespaceLower = purl.Namespace.ToLowerInvariant();
                    string packageNameLower = purl.Name.ToLowerInvariant();
                    HttpClient httpClient = CreateHttpClient();

                    return await GetHttpStringCache(httpClient, $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}/@v/{latestVersion}.mod", useCache);
                }
                else
                {
                    throw new Exception("Unable to enumerate verisons.");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching metadata: {0}", ex.Message);
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_GO_PKG_ENDPOINT}/{purl?.Namespace}/{purl?.Name}");
        }
    }
}