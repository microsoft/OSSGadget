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
        /// Check if the package exists in the respository.
        /// </summary>
        /// <param name="purl">The PackageURL to check.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <returns>True if the package is confirmed to exist in the repository. False otherwise.</returns>
        public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            return await CheckHttpCacheForPackage($"{ENV_CPAN_ENDPOINT}/release/{packageName}", useCache);
        }

        /// <summary>
        /// Download one CPAN package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }
            // Locate the URL
            string? packageVersionUrl = null;
            string? html = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/release/{packageName}");
            HtmlParser parser = new();
            AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);
            foreach (AngleSharp.Dom.IElement option in document.QuerySelectorAll("div.release select.extend option"))
            {
                if (!option.HasAttribute("value"))
                {
                    continue;
                }
                string? value = option.GetAttribute("value");
                string version = value.Split('-').Last();
                if (version.StartsWith("v", StringComparison.InvariantCultureIgnoreCase))
                {
                    version = version[1..];
                }
                Logger.Trace("Comparing {0} to {1}", version, packageVersion);

                if (version == packageVersion)
                {
                    // Now load the actual page so we can get the download URL
                    packageVersionUrl = $"{ENV_CPAN_ENDPOINT}/release/{value}";
                    break;
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
            foreach (AngleSharp.Dom.IElement? italic in document.QuerySelectorAll("li a i.fa-download"))
            {
                AngleSharp.Dom.IElement? anchor = italic.Closest("a");
                if (!anchor.TextContent.Contains("Download ("))
                {
                    continue;
                }

                string? binaryUrl = anchor.GetAttribute("href");
                System.Net.Http.HttpResponseMessage? result = await WebClient.GetAsync(binaryUrl);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                string targetName = $"cpan-{packageName}@{packageVersion}";
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
            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string packageName = purl.Name;
                List<string> versionList = new();

                string? html = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/release/{packageName}");
                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);
                foreach (AngleSharp.Dom.IElement option in document.QuerySelectorAll("div.release select.extend option"))
                {
                    if (!option.HasAttribute("value"))
                    {
                        continue;
                    }
                    string? value = option.GetAttribute("value");
                    Match? match = Regex.Match(value, @".*-([^-]+)$");
                    if (match.Success)
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, match.Groups[1].Value);
                        versionList.Add(match.Groups[1].Value);
                    }
                }

                IEnumerable<string> result = SortVersions(versionList.Distinct());
                return result;
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                string? packageName = purl.Name;
                if (packageName != null)
                {
                    string? contentRelease = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/release/{packageName}");
                    string? contentPod = await GetHttpStringCache($"{ENV_CPAN_ENDPOINT}/pod/{packageName.Replace("-", "::")}");
                    return contentRelease + "\n" + contentPod;
                }
                else
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching CPAN metadata: {0}", ex.Message);
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageName = purl?.Name;
            return new Uri($"{ENV_CPAN_ENDPOINT}/pod/{packageName}");
            // TODO: Add version support
        }
    }
}