// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class UbuntuProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_UBUNTU_ARCHIVE_MIRROR = "https://mirror.math.princeton.edu/pub";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_UBUNTU_ENDPOINT = "https://packages.ubuntu.com";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_UBUNTU_POOL_NAMES = "main,universe,multiverse,restricted";

        public UbuntuProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one VS Marketplace package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> the path or file written. </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageVersion = purl?.Version;
            var downloadedPaths = new List<string>();
            var downloadedUrls = new HashSet<string>();

            if (purl == null || purl.Name == null || packageVersion == null)
            {
                return downloadedPaths;
            }

            var availablePools = await GetPoolsForProject(purl);
            foreach (var pool in availablePools)
            {
                var archiveBaseUrl = await GetArchiveBaseUrlForProject(purl, pool);
                if (archiveBaseUrl == null)
                {
                    Logger.Debug("Unable to find archive base URL for {0}, pool {1}", purl.ToString(), pool);
                    continue;
                }

                try
                {
                    var html = await GetHttpStringCache(archiveBaseUrl, neverThrow: true);
                    if (html == null)
                    {
                        Logger.Debug("Error reading {0}", archiveBaseUrl);
                        continue;
                    }

                    var document = await new HtmlParser().ParseDocumentAsync(html);
                    foreach (var anchor in document.QuerySelectorAll("a"))
                    {
                        var anchorHref = anchor.GetAttribute("href");
                        if (anchorHref.Contains(packageVersion) && anchorHref.EndsWith(".deb"))
                        {
                            var fullDownloadUrl = archiveBaseUrl + "/" + anchorHref;
                            if (!downloadedUrls.Add(fullDownloadUrl))
                            {
                                // Never re-download the same file twice.
                                continue;
                            }
                            Logger.Debug("Downloading binary: {0}", fullDownloadUrl);

                            var downloadResult = await WebClient.GetAsync(fullDownloadUrl);
                            if (!downloadResult.IsSuccessStatusCode)
                            {
                                Logger.Debug("Error {0} downloading file {1}", downloadResult.StatusCode, fullDownloadUrl);
                                continue;
                            }

                            // TODO: Add distro version id
                            var targetName = $"ubuntu-{purl.Name}@{packageVersion}-{anchorHref}";
                            string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                            if (doExtract && Directory.Exists(extractionPath) && cached == true)
                            {
                                downloadedPaths.Add(extractionPath);
                                return downloadedPaths;
                            }

                            if (doExtract)
                            {
                                downloadedPaths.Add(await ExtractArchive(targetName, await downloadResult.Content.ReadAsByteArrayAsync(), cached));
                            }
                            else
                            {
                                targetName += Path.GetExtension(anchorHref) ?? "";
                                await File.WriteAllBytesAsync(targetName, await downloadResult.Content.ReadAsByteArrayAsync());
                                downloadedPaths.Add(targetName);
                            }
                        }

                        // Source Code URLs don't have the full version on the source files. We need to find
                        // them in the .dsc
                        else if (anchorHref.Contains(packageVersion) && anchorHref.EndsWith(".dsc"))
                        {
                            var dscContent = await GetHttpStringCache(archiveBaseUrl + "/" + anchorHref);
                            if (dscContent == null)
                            {
                                continue;
                            }

                            var seenFiles = new HashSet<string>();
                            foreach (Match match in Regex.Matches(dscContent, "^ [a-z0-9]+ \\d+ (.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => x != null))
                            {
                                seenFiles.Add(match.Groups[1].Value.Trim());
                            }

                            // Now we need to go through the anchor tags again looking for the source code files
                            foreach (var secondAnchor in document.QuerySelectorAll("a"))
                            {
                                var secondHref = secondAnchor.GetAttribute("href");
                                if (seenFiles.Any(f => f.Equals(secondHref) && !secondHref.EndsWith(".deb") && !secondHref.EndsWith(".dsc") && !secondHref.EndsWith(".asc")))
                                {
                                    var fullDownloadUrl = archiveBaseUrl + "/" + secondHref;
                                    if (!downloadedUrls.Add(fullDownloadUrl))
                                    {
                                        // Never re-download the same file twice.
                                        continue;
                                    }
                                    Logger.Debug("Downloading source code: {0}", fullDownloadUrl);

                                    var downloadResult = await WebClient.GetAsync(fullDownloadUrl);
                                    if (!downloadResult.IsSuccessStatusCode)
                                    {
                                        Logger.Debug("Error {0} downloading file {1}", downloadResult.StatusCode, fullDownloadUrl);
                                        continue;
                                    }

                                    // TODO: Add distro version id
                                    var targetName = $"ubuntu-{purl.Name}@{packageVersion}-{secondHref}";

                                    if (doExtract)
                                    {
                                        downloadedPaths.Add(await ExtractArchive(targetName, await downloadResult.Content.ReadAsByteArrayAsync(), cached));
                                    }
                                    else
                                    {
                                        targetName += Path.GetExtension(anchorHref) ?? "";
                                        await File.WriteAllBytesAsync(targetName, await downloadResult.Content.ReadAsByteArrayAsync());
                                        downloadedPaths.Add(targetName);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug("Error downloading binary for {0}: {1}", purl.ToString(), ex.Message);
                }
            }

            return downloadedPaths;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());

            var versionList = new List<string>();
            if (purl == null || purl.Name == null)
            {
                return versionList;
            }
            var availablePools = await GetPoolsForProject(purl);
            foreach (var pool in availablePools)
            {
                var archiveBaseUrl = await GetArchiveBaseUrlForProject(purl, pool);
                if (archiveBaseUrl == null)
                {
                    Logger.Debug("Unable to find archive base URL.");
                    continue;
                }

                Logger.Debug("Located archive base URL: {0}", archiveBaseUrl);

                // Now load the archive page, which will show all of the versions in each of the .dsc files there.
                try
                {
                    var html = await GetHttpStringCache(archiveBaseUrl, neverThrow: true);
                    if (html == null)
                    {
                        continue;
                    }
                    var document = await new HtmlParser().ParseDocumentAsync(html);
                    foreach (var anchor in document.QuerySelectorAll("a"))
                    {
                        var anchorHref = anchor.GetAttribute("href");
                        if (anchorHref.EndsWith(".dsc"))
                        {
                            Logger.Debug("Found a .dsc file: {0}", anchorHref);
                            var dscContent = await GetHttpStringCache(archiveBaseUrl + "/" + anchorHref);
                            foreach (var line in dscContent?.Split(new char[] { '\n', '\r' }) ?? Array.Empty<string>())
                            {
                                if (line.StartsWith("Version:"))
                                {
                                    var versionOnly = line.Replace("Version:", "").Trim();
                                    Match match = Regex.Match(versionOnly, "^\\d+:(.*)$");
                                    if (match.Success)
                                    {
                                        versionOnly = match.Groups[1].Value;
                                    }

                                    Logger.Debug("Identified a version: {0}", versionOnly);
                                    versionList.Add(versionOnly);
                                    break;  // Only care about the first version
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Error enumerating Ubuntu package versions: {0}", ex.Message);
                    throw;
                }
            }
            return SortVersions(versionList.Distinct());
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            Logger.Trace("GetMetadata {0}", purl?.ToString());

            if (purl == null || purl.Name == null)
            {
                return string.Empty;
            }

            var metadataContent = new StringBuilder();

            foreach (var distroUrlPrefix in GetBaseURLs(purl))
            {
                try
                {
                    var html = await GetHttpStringCache(distroUrlPrefix, neverThrow: true);
                    if (html != null)
                    {
                        var document = await new HtmlParser().ParseDocumentAsync(html);
                        foreach (var anchor in document.QuerySelectorAll("a"))
                        {
                            var anchorHref = anchor.GetAttribute("href");
                            if (anchorHref.EndsWith(".dsc"))
                            {
                                Logger.Debug("Found a .dsc file: {0}", anchorHref);
                                var dscContent = await GetHttpStringCache(distroUrlPrefix + anchorHref, neverThrow: true);
                                if (dscContent == null)
                                {
                                    continue;
                                }
                                metadataContent.AppendLine(dscContent);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug("Error obtaining .dsc file for {0}: {1}", purl.ToString(), ex.Message);
                }

                // Fallback to packages.ubuntu.com if we haven't seen any .dsc files
                if (metadataContent.Length == 0)
                {
                    try
                    {
                        var searchResults = await GetHttpStringCache($"{ENV_UBUNTU_ENDPOINT}/search?keywords={purl.Name}&searchon=names&exact=1&suite=all&section=all");
                        var parser = new HtmlParser();
                        var document = await parser.ParseDocumentAsync(searchResults);
                        var anchorItems = document.QuerySelectorAll("a.resultlink");
                        var metadataUrlList = anchorItems.Select(s => s.GetAttribute("href") ?? "");

                        foreach (var metadataUrl in metadataUrlList)
                        {
                            metadataContent.AppendLine(await GetHttpStringCache($"{ENV_UBUNTU_ENDPOINT}/{metadataUrl}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex, "Error fetching Ubuntu metadata: {0}", ex.Message);
                    }
                }
            }

            return metadataContent.ToString();
        }

        public override Uri? GetPackageAbsoluteUri(PackageURL purl)
        {
            var availablePools = GetPoolsForProject(purl).Result;
            foreach (var pool in availablePools)
            {
                var archiveBaseUrl = GetArchiveBaseUrlForProject(purl, pool).Result;
                if (archiveBaseUrl != null)
                {
                    return new Uri(archiveBaseUrl);
                }
            }
            return null;
        }

        /// <summary>
        ///     Identifies the base URL for package source files.
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="pool"> </param>
        /// <returns> </returns>
        private async Task<string?> GetArchiveBaseUrlForProject(PackageURL purl, string pool)
        {
            try
            {
                var html = await GetHttpStringCache($"{ENV_UBUNTU_ENDPOINT}/{pool}/{purl.Name}", neverThrow: true);
                if (html == null)
                {
                    return null;
                }
                var document = await new HtmlParser().ParseDocumentAsync(html);
                foreach (var anchor in document.QuerySelectorAll("a"))
                {
                    var href = anchor.GetAttribute("href");
                    if (href != null && href.EndsWith(".dsc"))
                    {
                        var match = Regex.Match(href, "(.+)/[^/]+\\.dsc");
                        if (match.Success)
                        {
                            return match.Groups[1].Value.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching Ubuntu archive base URL for {0}: {1}", purl.ToString(), ex.Message);
            }
            return null;
        }

        private List<string> GetBaseURLs(PackageURL purl)
        {
            var results = new List<string>();
            var dirName = string.Empty;
            if (purl.Name is string purlName)
            {
                dirName = purlName.StartsWith("lib") ? "lib" + purlName.Substring(3, 1) : purlName.Substring(0, 1);
            }
            else
            {
                return results;
            }

            var distroName = "ubuntu";  // default
            purl.Qualifiers?.TryGetValue("distro", out distroName);

            if (purl.Qualifiers != null && purl.Qualifiers.TryGetValue("pool", out string? selectedPool))
            {
                results.Add($"{ENV_UBUNTU_ARCHIVE_MIRROR}/{distroName}/pool/{selectedPool}/{dirName}/{purl.Name}/");
            }
            else
            {
                foreach (var pool in ENV_UBUNTU_POOL_NAMES.Split(","))
                {
                    results.Add($"{ENV_UBUNTU_ARCHIVE_MIRROR}/{distroName}/pool/{pool}/{dirName}/{purl.Name}/");
                    Logger.Debug($"{ENV_UBUNTU_ARCHIVE_MIRROR}/{distroName}/pool/{pool}/{dirName}/{purl.Name}/");
                }
            }
            return results;
        }

        /// <summary>
        ///     Identifies the available pools for a given Ubuntu project. For example, 'xenial'.
        /// </summary>
        /// <param name="purl"> Package URL to look up (only name is used). </param>
        /// <returns> List of pool names </returns>
        private async Task<IEnumerable<string>> GetPoolsForProject(PackageURL purl)
        {
            var pools = new HashSet<string>();
            try
            {
                var searchResults = await GetHttpStringCache($"{ENV_UBUNTU_ENDPOINT}/search?keywords={purl.Name}&searchon=names&exact=1&suite=all&section=all", neverThrow: true);
                var document = await new HtmlParser().ParseDocumentAsync(searchResults);
                foreach (var anchor in document.QuerySelectorAll("a.resultlink"))
                {
                    var href = anchor.GetAttribute("href");
                    if (href != null)
                    {
                        var match = Regex.Match(href, "^/([^/]+)/.+");
                        if (match.Success)
                        {
                            var pool = match.Groups[1].Value.Trim();
                            Logger.Debug("Identified pool: {0}", pool);
                            pools.Add(pool);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching Ubuntu pools for {0}: {1}", purl.ToString(), ex.Message);
            }
            return pools;
        }
    }
}