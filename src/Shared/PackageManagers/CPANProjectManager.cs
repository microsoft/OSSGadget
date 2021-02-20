// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class CPANProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CPAN_BINARY_ENDPOINT = "https://cpan.metacpan.org";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CPAN_ENDPOINT = "https://metacpan.org";

        public CPANProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one CPAN package and extract it to the target directory.
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
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            // Locate the URL
            string? packageVersionUrl = null;
            var html = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/release/{packageName}");
            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(html);
            foreach (var option in document.QuerySelectorAll("div.release select.extend option"))
            {
                if (!option.HasAttribute("value"))
                {
                    continue;
                }
                var value = option.GetAttribute("value");
                var version = value.Split('-').Last();
                if (version.StartsWith("v", StringComparison.InvariantCultureIgnoreCase))
                {
                    version = version.Substring(1);
                }
                Logger.Trace("Comparing {0} to {1}", version, packageVersion);

                if (version == packageVersion)
                {
                    packageVersionUrl = $"{ENV_CPAN_ENDPOINT}{value}";
                }
            }

            if (packageVersionUrl == null)
            {
                Logger.Debug($"Unable to find CPAN package {packageName}@{packageVersion}.");
                return downloadedPaths;
            }

            Logger.Debug($"Downloading {packageVersionUrl}");

            html = await GetHttpStringCache(packageVersionUrl);
            document = await parser.ParseDocumentAsync(html);
            foreach (var italic in document.QuerySelectorAll("li a i.fa-download"))
            {
                var anchor = italic.Closest("a");
                if (!anchor.TextContent.Contains("Download ("))
                {
                    continue;
                }

                var binaryUrl = anchor.GetAttribute("href");
                var result = await WebClient.GetAsync(binaryUrl);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                var targetName = $"cpan-{packageName}@{packageVersion}";
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
                    targetName += Path.GetExtension(binaryUrl) ?? "";
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(targetName);
                }
                break;
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

                var html = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/release/{packageName}");
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(html);
                foreach (var option in document.QuerySelectorAll("div.release select.extend option"))
                {
                    if (!option.HasAttribute("value"))
                    {
                        continue;
                    }
                    var value = option.GetAttribute("value");
                    var match = Regex.Match(value, @".*-([^-]+)$");
                    if (match.Success)
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, match.Groups[1].Value);
                        versionList.Add(match.Groups[1].Value);
                    }
                }

                var result = SortVersions(versionList.Distinct());
                return result;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error enumerating CPAN package: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/release/{packageName}");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching CPAN metadata: {0}", ex.Message);
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            var packageName = purl?.Name;
            return new Uri($"{ENV_CPAN_ENDPOINT}/pod/{packageName}");
            // TODO: Add version support
        }
    }
}