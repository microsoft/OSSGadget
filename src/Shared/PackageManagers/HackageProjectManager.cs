// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;

namespace Microsoft.CST.OpenSource.Shared
{
    class HackageProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_HACKAGE_ENDPOINT = "https://hackage.haskell.org";

        /// <summary>
        /// Download one Hackage (Haskell) package and extract it to the target directory.
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
                var url = $"{ENV_HACKAGE_ENDPOINT}/package/{packageName}-{packageVersion}/{packageName}-{packageVersion}.tar.gz";
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl.ToString());

                var targetName = $"hackage-{packageName}@{packageVersion}";
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
                Logger.Error(ex, "Error downloading Hackage package: {0}", ex.Message);
                downloadedPath = null;
            }
            return downloadedPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                Console.WriteLine($"{ENV_HACKAGE_ENDPOINT}/package/{packageName}");
                var html = await WebClient.GetAsync($"{ENV_HACKAGE_ENDPOINT}/package/{packageName}");
                html.EnsureSuccessStatusCode();
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                var ths = document.QuerySelectorAll("th");
                var versionList = new List<string>();
                foreach (var th in ths)
                {
                    if (th.TextContent.StartsWith("Versions"))
                    {
                        var td = th.NextElementSibling;
                        foreach (var version in td.QuerySelectorAll("a,strong"))
                        {
                            var versionString = version.TextContent.ToLower().Trim();
                            Logger.Debug("Identified {0} version {1}.", packageName, versionString);
                            versionList.Add(versionString);
                        }
                        break;
                    }
                }

                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating Hackage package: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_HACKAGE_ENDPOINT}/package/{packageName}");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching Hackage metadata: {ex.Message}");
                return null;
            }
        }
    }
}
