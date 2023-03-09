// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using AngleSharp.Html.Parser;
    using Helpers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class UbuntuProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "ubuntu";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_UBUNTU_ARCHIVE_MIRROR { get; set; } = "https://mirror.math.princeton.edu/pub";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_UBUNTU_ENDPOINT { get; set; } = "https://packages.ubuntu.com";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_UBUNTU_POOL_NAMES { get; set; } = "main,universe,multiverse,restricted";

        public UbuntuProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public UbuntuProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one VS Marketplace package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> the path or file written. </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            List<string> downloadedPaths = new();
            HashSet<string> downloadedUrls = new();
            HttpClient httpClient = CreateHttpClient();

            if (purl == null || purl.Name == null || purl.Version == null)
            {
                return downloadedPaths;
            }

            string packageVersion = purl.Version;

            IEnumerable<string>? availablePools = await GetPoolsForProject(purl);
            foreach (string? pool in availablePools)
            {
                string? archiveBaseUrl = await GetArchiveBaseUrlForProject(purl, pool);
                if (archiveBaseUrl == null)
                {
                    Logger.Debug("Unable to find archive base URL for {0}, pool {1}", purl.ToString(), pool);
                    continue;
                }

                try
                {
                    string? html = await GetHttpStringCache(httpClient, archiveBaseUrl, neverThrow: true);
                    if (html == null)
                    {
                        Logger.Debug("Error reading {0}", archiveBaseUrl);
                        continue;
                    }

                    AngleSharp.Html.Dom.IHtmlDocument document = await new HtmlParser().ParseDocumentAsync(html);
                    foreach (AngleSharp.Dom.IElement anchor in document.QuerySelectorAll("a"))
                    {
                        string? anchorHref = anchor.GetAttribute("href");
                        if (anchorHref.Contains(packageVersion) && anchorHref.EndsWith(".deb"))
                        {
                            string? fullDownloadUrl = archiveBaseUrl + "/" + anchorHref;
                            if (!downloadedUrls.Add(fullDownloadUrl))
                            {
                                // Never re-download the same file twice.
                                continue;
                            }
                            Logger.Debug("Downloading binary: {0}", fullDownloadUrl);

                            System.Net.Http.HttpResponseMessage downloadResult = await httpClient.GetAsync(fullDownloadUrl);
                            if (!downloadResult.IsSuccessStatusCode)
                            {
                                Logger.Debug("Error {0} downloading file {1}", downloadResult.StatusCode, fullDownloadUrl);
                                continue;
                            }

                            // TODO: Add distro version id
                            string targetName = $"ubuntu-{purl.Name}@{packageVersion}-{anchorHref}";
                            string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                            if (doExtract && Directory.Exists(extractionPath) && cached == true)
                            {
                                downloadedPaths.Add(extractionPath);
                                return downloadedPaths;
                            }

                            if (doExtract)
                            {
                                downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await downloadResult.Content.ReadAsStreamAsync(), cached));
                            }
                            else
                            {
                                extractionPath += Path.GetExtension(anchorHref) ?? "";
                                await File.WriteAllBytesAsync(extractionPath, await downloadResult.Content.ReadAsByteArrayAsync());
                                downloadedPaths.Add(extractionPath);
                            }
                        }

                        // Source Code URLs don't have the full version on the source files. We need to find
                        // them in the .dsc
                        else if (anchorHref.Contains(packageVersion) && anchorHref.EndsWith(".dsc"))
                        {
                            string? dscContent = await GetHttpStringCache(httpClient, archiveBaseUrl + "/" + anchorHref);
                            if (dscContent == null)
                            {
                                continue;
                            }

                            HashSet<string> seenFiles = new();
                            foreach (Match match in Regex.Matches(dscContent, "^ [a-z0-9]+ \\d+ (.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => x != null))
                            {
                                seenFiles.Add(match.Groups[1].Value.Trim());
                            }

                            // Now we need to go through the anchor tags again looking for the source code files
                            foreach (AngleSharp.Dom.IElement? secondAnchor in document.QuerySelectorAll("a"))
                            {
                                string? secondHref = secondAnchor.GetAttribute("href");
                                if (seenFiles.Any(f => f.Equals(secondHref) && !secondHref.EndsWith(".deb") && !secondHref.EndsWith(".dsc") && !secondHref.EndsWith(".asc")))
                                {
                                    string fullDownloadUrl = archiveBaseUrl + "/" + secondHref;
                                    if (!downloadedUrls.Add(fullDownloadUrl))
                                    {
                                        // Never re-download the same file twice.
                                        continue;
                                    }
                                    Logger.Debug("Downloading source code: {0}", fullDownloadUrl);

                                    System.Net.Http.HttpResponseMessage downloadResult = await httpClient.GetAsync(fullDownloadUrl);
                                    if (!downloadResult.IsSuccessStatusCode)
                                    {
                                        Logger.Debug("Error {0} downloading file {1}", downloadResult.StatusCode, fullDownloadUrl);
                                        continue;
                                    }

                                    // TODO: Add distro version id
                                    string targetName = $"ubuntu-{purl.Name}@{packageVersion}-{secondHref}";
                                    string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);

                                    if (doExtract)
                                    {
                                        downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await downloadResult.Content.ReadAsStreamAsync(), cached));
                                    }
                                    else
                                    {
                                        extractionPath += Path.GetExtension(anchorHref) ?? "";
                                        await File.WriteAllBytesAsync(extractionPath, await downloadResult.Content.ReadAsByteArrayAsync());
                                        downloadedPaths.Add(extractionPath);
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

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());

            if (purl == null || purl.Name == null)
            {
                return Array.Empty<string>();
            }
            List<string> versionList = new();
            IEnumerable<string> availablePools = await GetPoolsForProject(purl);
            HttpClient httpClient = CreateHttpClient();
            foreach (string pool in availablePools)
            {
                string? archiveBaseUrl = await GetArchiveBaseUrlForProject(purl, pool);
                if (archiveBaseUrl == null)
                {
                    Logger.Debug("Unable to find archive base URL.");
                    continue;
                }

                Logger.Debug("Located archive base URL: {0}", archiveBaseUrl);

                // Now load the archive page, which will show all of the versions in each of the .dsc files there.
                try
                {
                    string? html = await GetHttpStringCache(httpClient, archiveBaseUrl, useCache: useCache, neverThrow: true);
                    if (html == null)
                    {
                        continue;
                    }
                    AngleSharp.Html.Dom.IHtmlDocument? document = await new HtmlParser().ParseDocumentAsync(html);
                    foreach (AngleSharp.Dom.IElement? anchor in document.QuerySelectorAll("a"))
                    {
                        string? anchorHref = anchor.GetAttribute("href");
                        if (anchorHref.EndsWith(".dsc"))
                        {
                            Logger.Debug("Found a .dsc file: {0}", anchorHref);
                            string? dscContent = await GetHttpStringCache(httpClient, archiveBaseUrl + "/" + anchorHref);
                            foreach (string line in dscContent?.Split(new char[] { '\n', '\r' }) ?? Array.Empty<string>())
                            {
                                if (line.StartsWith("Version:"))
                                {
                                    string versionOnly = line.Replace("Version:", "").Trim();
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
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Logger.Debug("Unable to enumerate versions (404): {0}", ex.Message);
                    return Array.Empty<string>();
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Error enumerating Ubuntu package versions: {0}", ex.Message);
                    throw;
                }
            }
            return SortVersions(versionList.Distinct());
        }

        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("GetMetadata {0}", purl?.ToString());

            if (purl == null || purl.Name == null)
            {
                return null;
            }

            StringBuilder metadataContent = new();
            HttpClient httpClient = CreateHttpClient();
            foreach (string distroUrlPrefix in GetBaseURLs(purl))
            {
                try
                {
                    string? html = await GetHttpStringCache(httpClient, distroUrlPrefix, useCache: useCache, neverThrow: true);
                    if (html != null)
                    {
                        AngleSharp.Html.Dom.IHtmlDocument? document = await new HtmlParser().ParseDocumentAsync(html);
                        foreach (AngleSharp.Dom.IElement? anchor in document.QuerySelectorAll("a"))
                        {
                            string? anchorHref = anchor.GetAttribute("href");
                            if (anchorHref.EndsWith(".dsc"))
                            {
                                Logger.Debug("Found a .dsc file: {0}", anchorHref);
                                string? dscContent = await GetHttpStringCache(httpClient, distroUrlPrefix + anchorHref, neverThrow: true);
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
                        string? searchResults = await GetHttpStringCache(httpClient, $"{ENV_UBUNTU_ENDPOINT}/search?keywords={purl.Name}&searchon=names&exact=1&suite=all&section=all", useCache);
                        HtmlParser parser = new();
                        AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(searchResults);
                        AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> anchorItems = document.QuerySelectorAll("a.resultlink");
                        IEnumerable<string> metadataUrlList = anchorItems.Select(s => s.GetAttribute("href") ?? "");

                        foreach (string metadataUrl in metadataUrlList)
                        {
                            metadataContent.AppendLine(await GetHttpStringCache(httpClient, $"{ENV_UBUNTU_ENDPOINT}/{metadataUrl}"));
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
            IEnumerable<string> availablePools = GetPoolsForProject(purl).Result;
            foreach (string? pool in availablePools)
            {
                string? archiveBaseUrl = GetArchiveBaseUrlForProject(purl, pool).Result;
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
                HttpClient httpClient = CreateHttpClient();

                string? html = await GetHttpStringCache(httpClient, $"{ENV_UBUNTU_ENDPOINT}/{pool}/{purl.Name}", neverThrow: true);
                if (html == null)
                {
                    return null;
                }
                AngleSharp.Html.Dom.IHtmlDocument document = await new HtmlParser().ParseDocumentAsync(html);
                foreach (AngleSharp.Dom.IElement anchor in document.QuerySelectorAll("a"))
                {
                    string? href = anchor.GetAttribute("href");
                    if (href != null && href.EndsWith(".dsc"))
                    {
                        Match match = Regex.Match(href, "(.+)/[^/]+\\.dsc");
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
            List<string> results = new();
            string dirName;
            if (purl.Name is string purlName)
            {
                dirName = purlName.StartsWith("lib") ? "lib" + purlName.Substring(3, 1) : purlName.Substring(0, 1);
            }
            else
            {
                return results;
            }

            string? distroName = "ubuntu";  // default
            purl.Qualifiers?.TryGetValue("distro", out distroName);

            if (purl.Qualifiers != null && purl.Qualifiers.TryGetValue("pool", out string? selectedPool))
            {
                results.Add($"{ENV_UBUNTU_ARCHIVE_MIRROR}/{distroName}/pool/{selectedPool}/{dirName}/{purl.Name}/");
            }
            else
            {
                foreach (string? pool in ENV_UBUNTU_POOL_NAMES.Split(","))
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
            HashSet<string> pools = new();
            try
            {
                HttpClient httpClient = CreateHttpClient();

                string? searchResults = await GetHttpStringCache(httpClient, $"{ENV_UBUNTU_ENDPOINT}/search?keywords={purl.Name}&searchon=names&exact=1&suite=all&section=all", neverThrow: true);
                AngleSharp.Html.Dom.IHtmlDocument document = await new HtmlParser().ParseDocumentAsync(searchResults);
                foreach (AngleSharp.Dom.IElement anchor in document.QuerySelectorAll("a.resultlink"))
                {
                    string? href = anchor.GetAttribute("href");
                    if (href != null)
                    {
                        Match match = Regex.Match(href, "^/([^/]+)/.+");
                        if (match.Success)
                        {
                            string pool = match.Groups[1].Value.Trim();
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