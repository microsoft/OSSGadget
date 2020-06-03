// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    class CargoProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CARGO_ENDPOINT = "https://crates.io";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CARGO_ENDPOINT_STATIC = "https://static.crates.io";

        public CargoProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            var packageName = purl?.Name;
            return new Uri($"{ENV_CARGO_ENDPOINT}/crates/{packageName}");
            // TODO: Add version support
        }

        /// <summary>
        /// Download one Cargo package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>Path to the downloaded package</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract , bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            var fileName = purl?.ToStringFilename();
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion) || string.IsNullOrWhiteSpace(fileName))
            {
                Logger.Error("Error with 'purl' argument. Unable to download [{0} {1}] @ {2}. Both must be defined.", packageName, packageVersion, fileName);
                return downloadedPaths;
            }

            var url = $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}/{packageVersion}/download";
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

                if (WebClient == null)
                {
                    throw new NullReferenceException(nameof(WebClient));
                }
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();

                if (doExtract)
                {
                    downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync(), cached));
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
                Logger.Error(ex, "Error downloading Cargo package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <summary>
        /// Enumerates all possible versions of the package identified by purl.
        /// </summary>
        /// <param name="purl">Package URL specifying the package. Version is ignored.</param>
        /// <returns>A list of package versions</returns>
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            try
            {
                var packageName = purl.Name;
                var doc = await GetJsonCache($"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}");
                var versionList = new List<string>();
                foreach (var versionObject in doc.RootElement.GetProperty("versions").EnumerateArray())
                {
                    if (versionObject.TryGetProperty("num", out JsonElement version))
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, version.ToString());
                        versionList.Add(version.ToString());
                    }
                }
                return SortVersions(versionList.Distinct());

            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error enumerating Cargo package versions: {0}", ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gathers metadata (in no specific format) about the package.
        /// </summary>
        /// <param name="purl">Package URL for the package</param>
        /// <returns>Metadata as a string</returns>
        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error fetching Cargo metadata: {0}", ex.Message);
                return null;
            }
        }
    }
}
