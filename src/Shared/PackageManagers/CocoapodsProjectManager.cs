// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class CocoapodsProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_COCOAPODS_SPECS_ENDPOINT = "https://github.com/CocoaPods/Specs/tree/master";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_COCOAPODS_SPECS_RAW_ENDPOINT = "https://raw.githubusercontent.com/CocoaPods/Specs/master";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_COCOAPODS_METADATA_ENDPOINT = "https://cocoapods.org";

        public CocoapodsProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_COCOAPODS_METADATA_ENDPOINT}/pods/{purl.Name}");
        }

        /// <summary>
        ///     Download one Cocoapods package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            var fileName = purl?.ToStringFilename();
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion) || string.IsNullOrWhiteSpace(fileName))
            {
                Logger.Error("Error with 'purl' argument. Unable to download [{0} {1}] @ {2}. Both must be defined.", packageName, packageVersion, fileName);
                return downloadedPaths;
            }

            var prefix = GetCocoapodsPrefix(packageName);
            var podspec = await GetJsonCache($"{ENV_COCOAPODS_SPECS_RAW_ENDPOINT}/Specs/{prefix}/{packageName}/{packageVersion}/{packageName}.podspec.json");

            if (podspec.RootElement.TryGetProperty("source", out var source))
            {
                string? url = null;
                if (source.TryGetProperty("git", out var sourceGit) &&
                    source.TryGetProperty("tag", out var sourceTag))
                {
                    var sourceGitString = sourceGit.GetString();
                    var sourceTagString = sourceTag.GetString();

                    if (sourceGitString.EndsWith(".git"))
                    {
                        sourceGitString = sourceGitString[0..^4];
                    }
                    url = $"{sourceGitString}/archive/{sourceTagString}.zip";
                }
                else if (source.TryGetProperty("http", out var httpSource))
                {
                    url = httpSource.GetString();
                }

                if (url != null)
                {
                    Logger.Debug("Downloading {0}...", purl);
                    var result = await WebClient.GetAsync(url);
                    result.EnsureSuccessStatusCode();

                    var targetName = $"cocoapods-{fileName}";
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
                else
                {
                    Logger.Warn("Unable to find download location for {0}@{1}", packageName, packageVersion);
                }
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
                var prefix = GetCocoapodsPrefix(packageName ?? string.Empty);
                var html = await GetHttpStringCache($"{ENV_COCOAPODS_SPECS_ENDPOINT}/Specs/{prefix}/{packageName}");
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(html);
                var navItems = document.QuerySelectorAll("div.Details a.js-navigation-open");
                var versionList = new List<string>();

                foreach (var navItem in navItems)
                {
                    if (navItem.TextContent == "..")
                    {
                        continue;
                    }
                    Logger.Debug("Identified {0} version {1}.", packageName, navItem.TextContent);
                    versionList.Add(navItem.TextContent);
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating Cocoapods packages: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private string GetCocoapodsPrefix(string packageName)
        {
            var packageNameBytes = Encoding.UTF8.GetBytes(packageName);

            // The Cocoapods standard uses MD5(project name) as a prefix for sharing. There is no security
            // issue here, but we cannot use another algorithm.
#pragma warning disable SCS0006, CA5351, CA1308 // Weak hash, ToLowerInvarant()
            using var hashAlgorithm = MD5.Create();

            var prefixMD5 = BitConverter
                                .ToString(hashAlgorithm.ComputeHash(packageNameBytes))
                                .Replace("-", "")
                                .ToLowerInvariant()
                                .ToCharArray();
#pragma warning restore SCS0006, CA5351, CA1308 // Weak hash, ToLowerInvarant()

            var prefix = string.Format("{0}/{1}/{2}", prefixMD5[0], prefixMD5[1], prefixMD5[2]);
            return prefix;
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var cocoapodsWebContent = await GetHttpStringCache($"{ENV_COCOAPODS_METADATA_ENDPOINT}/pods/{packageName}");
                var podSpecContent = "";

                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(cocoapodsWebContent);
                var navItems = document.QuerySelectorAll("ul.links a");
                foreach (var navItem in navItems)
                {
                    if (navItem.TextContent == "See Podspec")
                    {
                        var url = navItem.GetAttribute("href");
                        url = url.Replace("https://github.com", "https://raw.githubusercontent.com");
                        url = url.Replace("/Specs/blob/master/", "/Specs/master/");
                        podSpecContent = await GetHttpStringCache(url);
                    }
                }
                return cocoapodsWebContent + " " + podSpecContent;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching Cocoapods metadata: {ex.Message}");
                return null;
            }
        }
    }
}