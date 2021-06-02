// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class CRANProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CRAN_ENDPOINT = "https://cran.r-project.org";

        public CRANProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one CRAN package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;

            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. All must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            // Current Version
            try
            {
                var url = $"{ENV_CRAN_ENDPOINT}/src/contrib/{packageName}_{packageVersion}.tar.gz";
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                var targetName = $"cran-{packageName}@{packageVersion}";
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
                    targetName += Path.GetExtension(url) ?? "";
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(targetName);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading CRAN package: {0}@{1}. Checking archives instead.", packageName, packageVersion);
            }
            if (downloadedPaths.Count > 0)
            {
                return downloadedPaths;
            }

            // Archive Version - Only continue here if needed
            try
            {
                var url = $"{ENV_CRAN_ENDPOINT}/src/contrib/Archive/{packageName}/{packageName}_{packageVersion}.tar.gz";
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                var targetName = $"cran-{packageName}@{packageVersion}";
                if (doExtract)
                {
                    downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync(), cached));
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
                Logger.Debug(ex, "Error downloading CRAN package: {0}", ex.Message);
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

            try
            {
                var packageName = purl.Name;
                var versionList = new List<string>();

                // Get the latest version
                var html = await WebClient.GetAsync($"{ENV_CRAN_ENDPOINT}/web/packages/{packageName}/index.html");
                html.EnsureSuccessStatusCode();
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                var tds = document.QuerySelectorAll("td");
                for (int i = 0; i < tds.Length; i++)
                {
                    if (tds[i].TextContent == "Version:")
                    {
                        var value = tds[i + 1]?.TextContent?.Trim();
                        if (value != null)
                        {
                            versionList.Add(value);
                        }
                        break;
                    }
                }

                // Get the remaining versions
                html = await WebClient.GetAsync($"{ENV_CRAN_ENDPOINT}/src/contrib/Archive/{packageName}/");
                html.EnsureSuccessStatusCode();
                document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                tds = document.QuerySelectorAll("a");
                foreach (var td in tds)
                {
                    var href = td.GetAttribute("href");
                    if (href.Contains(".tar.gz"))
                    {
                        var version = href.Replace(".tar.gz", "");
                        version = version.Replace(packageName + "_", "").Trim();
                        Logger.Debug("Identified {0} version {1}.", packageName, version);
                        versionList.Add(version);
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
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_CRAN_ENDPOINT}/web/packages/{packageName}/index.html");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching CRAN metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            // CRAN doesn't have a homepage for package version
            return new Uri($"{ENV_CRAN_ENDPOINT}/web/packages/{purl?.Name}/index.html");
        }
    }
}