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

        /// <summary>
        /// Download one Cargo package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>Path to the downloaded package</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract = true)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                var url = $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}/{packageVersion}/download";
                Logger.Debug("Downloading {0}", url);
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                var targetName = $"cargo-{packageName}@{packageVersion}";
                if (doExtract)
                {
                    downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync()));
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
        public override async Task<string> GetMetadata(PackageURL purl)
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
