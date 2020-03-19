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
        /// <returns>n/a</returns>
        public override async Task<string> DownloadVersion(PackageURL purl, bool doExtract = true)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            string downloadedPath = null;

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPath;
            }

            try
            {
                var url = $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}/{packageVersion}/download";
                Logger.Debug("Downloading {0}", url);
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);
                var targetName = $"cargo-{packageName}@{packageVersion}";
                if (doExtract)
                {
                    downloadedPath = await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync());
                }
                else
                {
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPath = targetName;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error downloading Cargo package: {0}", ex.Message);
                downloadedPath = null;
            }
            return downloadedPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
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
