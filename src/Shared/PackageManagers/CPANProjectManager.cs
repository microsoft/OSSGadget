// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;

namespace Microsoft.CST.OpenSource.Shared
{
    class CPANProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CPAN_ENDPOINT = "https://metacpan.org";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CPAN_BINARY_ENDPOINT = "https://cpan.metacpan.org";

        /// <summary>
        /// Download one CPAN package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<string> DownloadVersion(PackageURL purl)
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

            // Locate the URL
            string packageVersionUrl = null;
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
                if (version.StartsWith("v") || version.StartsWith("V"))
                {
                    version = version.Substring(1);
                }

                if (version == packageVersion)
                {
                    packageVersionUrl = $"{ENV_CPAN_ENDPOINT}{value}";
                }
            }

            if (packageVersionUrl == null)
            {
                Logger.Warn($"Unable to find CPAN package {packageName}@{packageVersion}.");
                return downloadedPath;
            }

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
                Logger.Debug("Downloading {0}...", purl.ToString());
                downloadedPath = await ExtractArchive($"cran-{packageName}@{packageVersion}", await result.Content.ReadAsByteArrayAsync());
                break;
            }
            return downloadedPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
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
                foreach (var v in result)
                {
                    Console.WriteLine(v);
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating CPAN package: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/release/{packageName}");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching CPAN metadata: {ex.Message}");
                return null;
            }
        }
    }
}
