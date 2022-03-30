// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Extensions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class CargoProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CARGO_ENDPOINT = "https://crates.io";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CARGO_ENDPOINT_STATIC = "https://static.crates.io";

        public CargoProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public CargoProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one Cargo package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> Path to the downloaded package </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            string? fileName = purl?.ToStringFilename();
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion) || string.IsNullOrWhiteSpace(fileName))
            {
                Logger.Debug("Error with 'purl' argument. Unable to download [{0} {1}] @ {2}. Both must be defined.", packageName, packageVersion, fileName);
                return downloadedPaths;
            }

            string url = $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}/{packageVersion}/download";
            try
            {
                string targetName = $"cargo-{fileName}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                // if the cache is already present, no need to extract
                if (doExtract && cached && Directory.Exists(extractionPath))
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }
                Logger.Debug("Downloading {0}", url);

                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();

                if (doExtract)
                {
                    downloadedPaths.Add(await ExtractArchive(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsByteArrayAsync(), cached));
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
                Logger.Debug(ex, "Error downloading Cargo package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
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
            return await CheckJsonCacheForPackage(httpClient, $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}", useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string? packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();
                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}");
                List<string> versionList = new();
                foreach (JsonElement versionObject in doc.RootElement.GetProperty("versions").EnumerateArray())
                {
                    if (versionObject.TryGetProperty("num", out JsonElement version))
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, version.ToString());
                        if (version.ToString() is string s)
                        {
                            versionList.Add(s);
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

        /// <inheritdoc />
        public override async Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
        {
            try
            {
                string? packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();
                string? content = await GetHttpStringCache(httpClient, $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}", useCache);
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching Cargo metadata: {0}", ex.Message);
                throw;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageName = purl?.Name;
            return new Uri($"{ENV_CARGO_ENDPOINT}/crates/{packageName}");
            // TODO: Add version support
        }
    }
}