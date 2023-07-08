// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;
    using Extensions;
    using Helpers;
    using Newtonsoft.Json.Linq;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class CocoapodsProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "cocoapods";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_COCOAPODS_SPECS_ENDPOINT { get; set; } = "https://github.com/CocoaPods/Specs/tree/master";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_COCOAPODS_SPECS_RAW_ENDPOINT { get; set; } = "https://raw.githubusercontent.com/CocoaPods/Specs/master";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_COCOAPODS_METADATA_ENDPOINT { get; set; } = "https://cocoapods.org";

        public CocoapodsProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

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
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            string? fileName = purl?.ToStringFilename();
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion) || string.IsNullOrWhiteSpace(fileName))
            {
                Logger.Debug("Error with 'purl' argument. Unable to download [{0} {1}] @ {2}. Both must be defined.", packageName, packageVersion, fileName);
                return downloadedPaths;
            }

            HttpClient httpClient = CreateHttpClient();
            string prefix = GetCocoapodsPrefix(packageName);
            System.Text.Json.JsonDocument podspec = await GetJsonCache(httpClient, $"{ENV_COCOAPODS_SPECS_RAW_ENDPOINT}/Specs/{prefix}/{packageName}/{packageVersion}/{packageName}.podspec.json");

            if (podspec.RootElement.TryGetProperty("source", out System.Text.Json.JsonElement source))
            {
                string? url = null;
                if (source.TryGetProperty("git", out System.Text.Json.JsonElement sourceGit) &&
                    source.TryGetProperty("tag", out System.Text.Json.JsonElement sourceTag))
                {
                    string? sourceGitString = sourceGit.GetString();
                    string? sourceTagString = sourceTag.GetString();

                    if (!string.IsNullOrWhiteSpace(sourceGitString) && sourceGitString.EndsWith(".git"))
                    {
                        sourceGitString = sourceGitString[0..^4];
                    }
                    url = $"{sourceGitString}/archive/{sourceTagString}.zip";
                }
                else if (source.TryGetProperty("http", out System.Text.Json.JsonElement httpSource))
                {
                    url = httpSource.GetString();
                }

                if (url != null)
                {
                    Logger.Debug("Downloading {0}...", purl);
                    System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                    result.EnsureSuccessStatusCode();

                    string targetName = $"cocoapods-{fileName}";
                    string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                    if (doExtract && Directory.Exists(extractionPath) && cached == true)
                    {
                        downloadedPaths.Add(extractionPath);
                        return downloadedPaths;
                    }
                    if (doExtract)
                    {
                        downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsStreamAsync(), cached));
                    }
                    else
                    {
                        extractionPath += Path.GetExtension(url) ?? "";
                        await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                        downloadedPaths.Add(extractionPath);
                    }
                }
                else
                {
                    Logger.Debug("Unable to find download location for {0}@{1}", packageName, packageVersion);
                }
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            string prefix = GetCocoapodsPrefix(packageName ?? string.Empty);
            HttpClient httpClient = CreateHttpClient();

            return await CheckHttpCacheForPackage(httpClient, $"{ENV_COCOAPODS_SPECS_ENDPOINT}/Specs/{prefix}/{packageName}", useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            try
            {
                string? packageName = purl.Name;
                string? prefix = GetCocoapodsPrefix(packageName ?? string.Empty);
                HttpClient httpClient = CreateHttpClient();
                string? html = await GetHttpStringCache(httpClient, $"{ENV_COCOAPODS_SPECS_ENDPOINT}/Specs/{prefix}/{packageName}");
                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);
                // Fetch the embedded react data
                string innerHtml = document.QuerySelector("script[data-target='react-app.embeddedData']").InnerHtml;
                // The contents of the script tag are JSON, so parse the innerHtml as a JObject
                JObject embeddedData = JObject.Parse(innerHtml);
                // use JsonPath to select the version numbers as JValues
                var versions = embeddedData.SelectTokens("$.payload.tree.items[*].name");
                List<string> versionList = new();

                // For each version add it to the list
                foreach (var version in versions)
                {
                    if (version is JValue jValue)
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, jValue.Value);
                        versionList.Add(jValue.Value.ToString());
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Debug("Unable to enumerate versions (404): {0}", ex.Message);
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        private string GetCocoapodsPrefix(string packageName)
        {
            byte[] packageNameBytes = Encoding.UTF8.GetBytes(packageName);

            // The Cocoapods standard uses MD5(project name) as a prefix for sharing. There is no security
            // issue here, but we cannot use another algorithm.
#pragma warning disable SCS0006, CA5351, CA1308 // Weak hash, ToLowerInvarant()
            using MD5 hashAlgorithm = MD5.Create(); // CodeQL [cs/weak-crypto] Required for compatibility with Cocoapods

            char[] prefixMD5 = BitConverter
                                .ToString(hashAlgorithm.ComputeHash(packageNameBytes))
                                .Replace("-", "")
                                .ToLowerInvariant()
                                .ToCharArray();
#pragma warning restore SCS0006, CA5351, CA1308 // Weak hash, ToLowerInvarant()

            string prefix = string.Format("{0}/{1}/{2}", prefixMD5[0], prefixMD5[1], prefixMD5[2]);
            return prefix;
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                HttpClient httpClient = CreateHttpClient();
                string? packageName = purl.Name;
                string? cocoapodsWebContent = await GetHttpStringCache(httpClient, $"{ENV_COCOAPODS_METADATA_ENDPOINT}/pods/{packageName}", useCache);
                string? podSpecContent = "";

                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(cocoapodsWebContent);
                AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> navItems = document.QuerySelectorAll("ul.links a");
                foreach (AngleSharp.Dom.IElement navItem in navItems)
                {
                    if (navItem.TextContent == "See Podspec")
                    {
                        string url = navItem.GetAttribute("href");
                        url = url.Replace("https://github.com", "https://raw.githubusercontent.com");
                        url = url.Replace("/Specs/blob/master/", "/Specs/master/");
                        podSpecContent = await GetHttpStringCache(httpClient, url);
                    }
                }
                return cocoapodsWebContent + " " + podSpecContent;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching Cocoapods metadata: {ex.Message}");
                return null;
            }
        }
    }
}