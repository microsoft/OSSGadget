// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    class ComposerProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_COMPOSER_ENDPOINT = "https://repo.packagist.org";

        /// <summary>
        /// Download one Composer (PHP) package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<string> DownloadVersion(PackageURL purl, bool doExtract = true)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = $"{purl?.Namespace}/{purl?.Name}";
            var packageVersion = purl?.Version;
            string downloadedPath = null;

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPath;
            }

            try
            {
                var doc = await GetJsonCache($"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json");
                foreach (var topObject in doc.RootElement.GetProperty("packages").EnumerateObject())
                {
                    foreach (var versionObject in topObject.Value.EnumerateObject())
                    {
                        if (versionObject.Name != packageVersion)
                        {
                            continue;
                        }
                        var url = versionObject.Value.GetProperty("dist").GetProperty("url").GetString();
                        var result = await WebClient.GetAsync(url);
                        result.EnsureSuccessStatusCode();
                        Logger.Debug("Downloading {0}...", purl);

                        var targetName = $"composer-{packageName}@{packageVersion}";
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
                }
                if (downloadedPath == null)
                {
                    Logger.Warn("Unable to find version to download.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error downloading Composer package: {ex.Message}");
                downloadedPath = null;
            }
            return downloadedPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            try
            {
                var packageName = $"{purl.Namespace}/{purl.Name}";
                var doc = await GetJsonCache($"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json");
                var versionList = new List<string>();
                foreach (var topObject in doc.RootElement.GetProperty("packages").EnumerateObject())
                {
                    foreach (var versionObject in topObject.Value.EnumerateObject())
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, versionObject.Name);
                        versionList.Add(versionObject.Name);
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating Composer package: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = $"{purl.Namespace}/{purl.Name}";
                var content = await GetHttpStringCache($"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching Composer metadata: {ex.Message}");
                return null;
            }
        }
    }
}
