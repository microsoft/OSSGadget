// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class ComposerProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_COMPOSER_ENDPOINT = "https://repo.packagist.org";

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

            var packageNamespace = purl?.Namespace;
            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName) ||
                string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1} {2}]. All three must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                var doc = await GetJsonCache($"{ENV_COMPOSER_ENDPOINT}/p/{packageNamespace}/{packageName}.json");
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

                        var fsNamespace = Utilities.NormalizeStringForFileSystem(packageNamespace);
                        var fsName = Utilities.NormalizeStringForFileSystem(packageName);
                        var fsVersion = Utilities.NormalizeStringForFileSystem(packageVersion);

                        var targetName = $"composer-{fsNamespace}-{fsName}@{fsVersion}";
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

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            var versionList = new List<string>();

            if (string.IsNullOrWhiteSpace(purl?.Namespace) || string.IsNullOrWhiteSpace(purl?.Name))
            {
                return versionList;
            }

            var packageName = $"{purl?.Namespace}/{purl?.Name}";

            try
            {
                var doc = await GetJsonCache($"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json");

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
                Logger.Warn("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = $"{purl.Namespace}/{purl.Name}";
                var content = await GetHttpStringCache($"{ENV_COMPOSER_ENDPOINT}/p/{packageName}.json");
                return content;
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