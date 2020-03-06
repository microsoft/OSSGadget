// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.OpenSource.Shared
{
    class PyPIProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_PYPI_ENDPOINT = "https://pypi.org";

        /// <summary>
        /// Download one PyPI package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<string> DownloadVersion(PackageURL purl)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            string downloadPath = null;

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return null;
            }

            try
            {
                var doc = await GetJsonCache($"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json");

                if (!doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    return null;
                }

                foreach (var versionObject in releases.EnumerateObject())
                {
                    if (versionObject.Name != packageVersion || downloadPath != null)
                    {
                        continue;
                    }
                    foreach (var release in versionObject.Value.EnumerateArray())
                    {
                        // For PyPI projects, we only download source distributions
                        if (release.GetProperty("packagetype").GetString() == "sdist")
                        {
                            var result = await WebClient.GetAsync(release.GetProperty("url").GetString());
                            result.EnsureSuccessStatusCode();
                            downloadPath = await ExtractArchive($"pypi-{packageName}@{packageVersion}", await result.Content.ReadAsByteArrayAsync());
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error downloading PyPI package: {0}", ex.Message);
                downloadPath = null;
            }
            return downloadPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var doc = await GetJsonCache($"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json");
                var versionList = new List<string>();
                if (doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    foreach (var versionObject in releases.EnumerateObject())
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, versionObject.Name);
                        versionList.Add(versionObject.Name);
                    }
                }

                // Add the current version (not included in releases)
                if (doc.RootElement.TryGetProperty("info", out JsonElement info) &&
                    info.TryGetProperty("version", out JsonElement version))
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, version.GetString());
                    versionList.Add(version.GetString());
                }

                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error enumerating PyPI packages: {0}", ex.Message);
                return Array.Empty<string>();
            }
        }


        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                return await GetHttpStringCache($"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/json");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error fetching PyPI metadata: {0}", ex.Message);
                return null;
            }
        }
    }
}
