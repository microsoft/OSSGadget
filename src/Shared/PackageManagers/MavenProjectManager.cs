// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using AngleSharp.Html.Parser;
    using Helpers;
    using Microsoft.CodeAnalysis.Sarif;
    using Microsoft.CST.OpenSource.Contracts;
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.Model.Enums;
    using Microsoft.CST.OpenSource.PackageActions;
    using Newtonsoft.Json;
    using PackageUrl;
    using PuppeteerSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class MavenProjectManager : TypedManager<IManagerPackageVersionMetadata, MavenProjectManager.MavenArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "maven";

        public override string ManagerType => Type;

        public const MavenSupportedUpstream DEFAULT_MAVEN_ENDPOINT = MavenSupportedUpstream.MavenCentralRepository;
        public MavenSupportedUpstream ENV_MAVEN_ENDPOINT { get; set; } = DEFAULT_MAVEN_ENDPOINT;

        public MavenProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null,
            TimeSpan? timeout = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory, timeout)
        {
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ArtifactUri<MavenArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            string? packageName = Check.NotNull(nameof(purl.Name), purl?.Name);
            string? packageVersion = Check.NotNull(nameof(purl.Version), purl?.Version);
            string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl();
            MavenSupportedUpstream upstream = feedUrl.GetMavenSupportedUpstream();

            if (upstream == MavenSupportedUpstream.MavenCentralRepository)
            {
                string? packageNamespace = Check.NotNull(nameof(purl.Namespace), purl?.Namespace).Replace('.', '/');
                var baseUrl = $"{upstream.GetRepositoryUrl().EnsureTrailingSlash()}{packageNamespace}/{packageName}/{packageVersion}/";

                HttpClient httpClient = CreateHttpClient();
                string? html = await GetHttpStringCache(httpClient, baseUrl, useCache);
                if (string.IsNullOrEmpty(html))
                {
                    throw new InvalidOperationException();
                }

                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);

                foreach (string fileName in document.QuerySelectorAll("a").Select(link => link.GetAttribute("href").ToString()))
                {
                    if (fileName == "../") continue;

                    MavenArtifactType artifactType = GetMavenArtifactType(fileName);
                    yield return new ArtifactUri<MavenArtifactType>(artifactType, baseUrl + fileName);
                }
            }
            else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
            {
                string packageNamespace = purl.Namespace;
                var packageVersionUrl = $"{upstream.GetRepositoryUrl()}{packageNamespace}:{packageName}:{purl.Version}";

                // Google Maven Repository's webpage has dynamic content
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();
                var browser = await Puppeteer.LaunchAsync(new LaunchOptions { HeadlessMode = HeadlessMode.True });
                var page = await browser.NewPageAsync();
                await page.GoToAsync(packageVersionUrl, WaitUntilNavigation.DOMContentLoaded);

                await page.WaitForSelectorAsync("tr");
                var trElement = await page.XPathAsync("//tr[td[contains(text(), 'Artifact(s)')]]");
                var anchors = await trElement.ElementAt(0).QuerySelectorAllAsync("a");

                foreach (var anchor in anchors)
                {
                    var hrefValue = await (await anchor.GetPropertyAsync("href")).JsonValueAsync<string>();
                    MavenArtifactType artifactType = GetMavenArtifactType(hrefValue);
                    yield return new ArtifactUri<MavenArtifactType>(artifactType, hrefValue);
                }

                await browser.CloseAsync();
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true)
        {
            // Packages by owner is not currently supported for Maven, so an empty list is returned. This is due to multiple registries
            // being supported, and this method not being able to support that.
            yield break;
        }

        /// <summary>
        /// Download one Maven package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());
            string? packageNamespace = purl?.Namespace?.Replace('.', '/');
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName) ||
                string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Warn("Unable to download [{0} {1} {2}]. All must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            IEnumerable<ArtifactUri<MavenArtifactType>> artifacts = (await GetArtifactDownloadUrisAsync(purl, useCache: cached).ToListAsync())
                .Where(artifact => artifact.Type is MavenArtifactType.Jar or MavenArtifactType.SourcesJar or MavenArtifactType.JavadocJar or MavenArtifactType.Aar);
            foreach (ArtifactUri<MavenArtifactType> artifact in artifacts)
            {
                try
                {
                    HttpClient httpClient = CreateHttpClient();

                    System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(artifact.Uri);
                    result.EnsureSuccessStatusCode();
                    Logger.Debug($"Downloading {purl}...");

                    string targetName = $"maven-{packageNamespace}-{packageName}{artifact.Type}@{packageVersion}";
                    targetName = targetName.Replace('/', '-');
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
                        extractionPath += artifact.Uri.GetExtension() ?? "";
                        await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                        downloadedPaths.Add(extractionPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error downloading Maven package: {0}", ex.Message);
                }
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name) || string.IsNullOrEmpty(purl.Namespace))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl();
            MavenSupportedUpstream upstream = feedUrl.GetMavenSupportedUpstream();

            HttpClient httpClient = CreateHttpClient();

            string packageRepositoryCheckUri = string.Empty;
            if (upstream == MavenSupportedUpstream.MavenCentralRepository)
            {
                string packageNamespace = purl.Namespace.Replace('.', '/');
                packageRepositoryCheckUri = $"{upstream.GetRepositoryUrl().EnsureTrailingSlash()}{packageNamespace}/{packageName}/maven-metadata.xml";
            }
            else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
            {
                string packageNamespace = purl.Namespace;
                packageRepositoryCheckUri = $"{upstream.GetRepositoryUrl()}{packageNamespace}:{packageName}";
            }

            return await CheckHttpCacheForPackage(httpClient, packageRepositoryCheckUri, useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl is null || purl.Name is null || purl.Namespace is null)
            {
                return new List<string>();
            }
            try
            {
                string packageName = purl.Name;
                string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl();
                MavenSupportedUpstream upstream = feedUrl.GetMavenSupportedUpstream();

                HttpClient httpClient = CreateHttpClient();

                string baseUrl = string.Empty;
                if (upstream == MavenSupportedUpstream.MavenCentralRepository)
                {
                    string packageNamespace = purl.Namespace.Replace('.', '/');
                    baseUrl = $"{upstream.GetRepositoryUrl().EnsureTrailingSlash()}{packageNamespace}/{packageName}/";
                }
                else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
                {
                    string packageNamespace = purl.Namespace;
                    baseUrl = $"{upstream.GetRepositoryUrl()}{packageNamespace}:{packageName}";
                }

                string? content = await GetHttpStringCache(httpClient, baseUrl, useCache);

                List<string> versionList = new();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new List<string>();
                }

                if (upstream == MavenSupportedUpstream.MavenCentralRepository)
                {
                    // Parse the html file.
                    HtmlParser parser = new();
                    AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(content);

                    // Break the version content down into its individual lines. Includes the parent directory and xml + hash files.
                    IEnumerable<string> htmlEntries = document.QuerySelector("#contents").QuerySelectorAll("a").Select(a => a.TextContent);

                    foreach (string htmlEntry in htmlEntries)
                    {
                        // Get the version.
                        if (htmlEntry.EndsWith('/') && !htmlEntry.Equals("../"))
                        {
                            var versionStr = htmlEntry.TrimEnd(htmlEntry[^1]);
                            Logger.Debug("Identified {0} version {1}.", packageName, versionStr);
                            versionList.Add(versionStr);
                        }
                    }
                }
                else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
                {
                    // Google Maven Repository's webpage has dynamic content
                    var browserFetcher = new BrowserFetcher();
                    await browserFetcher.DownloadAsync();
                    var browser = await Puppeteer.LaunchAsync(new LaunchOptions { HeadlessMode = HeadlessMode.True });
                    var page = await browser.NewPageAsync();
                    await page.GoToAsync(baseUrl, WaitUntilNavigation.DOMContentLoaded);

                    await page.WaitForSelectorAsync(".artifact-child-item");
                    var htmlEntries = await page.QuerySelectorAllAsync(".artifact-child-item");

                    // Get the version.
                    foreach (var htmlEntry in htmlEntries)
                    {
                        var span = await htmlEntry.QuerySelectorAsync("span");
                        var versionStr = await (await span.GetPropertyAsync("textContent")).JsonValueAsync<string>();
                        Logger.Debug("Identified {0} version {1}.", packageName, versionStr);
                        versionList.Add(versionStr);
                    }

                    await browser.CloseAsync();
                    return versionList.Distinct();
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

        /// <inheritdoc />
        public override async Task<bool> PackageVersionExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageVersionExists {0}", purl?.ToString());
            if (purl is null || purl.Name is null || purl.Namespace is null)
            {
                return false;
            }
            try
            {
                string packageName = purl.Name;
                string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl();
                MavenSupportedUpstream upstream = feedUrl.GetMavenSupportedUpstream();

                HttpClient httpClient = CreateHttpClient();

                string packageRepositoryCheckUri = string.Empty;
                if (upstream == MavenSupportedUpstream.MavenCentralRepository)
                {
                    string packageNamespace = purl.Namespace.Replace('.', '/');
                    packageRepositoryCheckUri = $"{upstream.GetRepositoryUrl().EnsureTrailingSlash()}{packageNamespace}/{packageName}/{purl.Version}/";
                }
                else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
                {
                    string packageNamespace = purl.Namespace;
                    packageRepositoryCheckUri = $"{upstream.GetRepositoryUrl()}{packageNamespace}:{packageName}:{purl.Version}";
                }

                string? content = await GetHttpStringCache(httpClient, packageRepositoryCheckUri, useCache);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return false;
                }

                return true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Debug("Package version doesn't exist (404): {0}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to check for version existence: {0}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                string? packageName = purl?.Name;
                string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl();
                MavenSupportedUpstream upstream = feedUrl.GetMavenSupportedUpstream();

                HttpClient httpClient = CreateHttpClient();

                string version;
                if (purl?.Version == null)
                {
                    // if no version is specified, use the earliest version available
                    var versions = await EnumerateVersionsAsync(purl, useCache);
                    if (!versions.Any())
                    {
                        throw new Exception("No version specified and unable to enumerate.");
                    }

                    version = versions.ElementAt(0);
                }
                else
                {
                    version = purl.Version;
                }

                if (upstream == MavenSupportedUpstream.MavenCentralRepository)
                {
                    string? packageNamespace = purl?.Namespace?.Replace('.', '/');
                    return await GetHttpStringCache(httpClient, $"{upstream.GetRepositoryUrl().EnsureTrailingSlash()}{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom", useCache);
                }
                else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
                {
                    string packageNamespace = purl.Namespace;
                    var packageVersionUrl = $"{upstream.GetRepositoryUrl()}{packageNamespace}:{packageName}:{version}";

                    var metadata = await GoogleMavenRepositoryMetadataParserHelperAsync(packageVersionUrl);
                    return JsonConvert.SerializeObject(metadata);
                }

                throw new Exception($"Unable to obtain metadata for {purl}.");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Error fetching Maven metadata: {ex.Message}");
                return null;
            }
        }

        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true, bool includeRepositoryMetadata = true)
        {
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return null; }

            PackageMetadata metadata = new();
            metadata.Name = purl.GetFullName();
            metadata.PackageVersion = purl?.Version;
            metadata.PackageManagerUri = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl()).EnsureTrailingSlash();
            metadata.Platform = "Maven";
            metadata.Language = "Java";

            metadata.UploadTime = await GetPackagePublishDateAsync(purl, useCache);

            return metadata;
        }

        public override Uri? GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageName = purl?.Name;
            string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl();
            MavenSupportedUpstream upstream = feedUrl.GetMavenSupportedUpstream();

            if (upstream == MavenSupportedUpstream.MavenCentralRepository)
            {
                string? packageNamespace = purl?.Namespace?.Replace('.', '/');
                return new Uri($"{upstream.GetRepositoryUrl().EnsureTrailingSlash()}{packageNamespace}/{packageName}");
            }
            else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
            {
                string packageNamespace = purl.Namespace;
                return new Uri($"{upstream.GetRepositoryUrl()}{packageNamespace}/{packageName}");
            }

            Logger.Warn($"Unable to get package absolute URI for {purl}.");
            return null;
        }

        internal async Task<Dictionary<string,string>> GoogleMavenRepositoryMetadataParserHelperAsync(string url)
        {
            var metadata = new Dictionary<string,string>();
            
            // Google Maven Repository's webpage has dynamic content. Scrape available metadata on the webpage.
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions { HeadlessMode = HeadlessMode.True });
            var page = await browser.NewPageAsync();
            await page.GoToAsync(url, WaitUntilNavigation.DOMContentLoaded);

            await page.WaitForSelectorAsync("tr");
            var fields = await page.QuerySelectorAllAsync("tr");

            foreach (var field in fields)
            {
                var tds = await field.QuerySelectorAllAsync("td");

                string key = string.Empty;
                string value = string.Empty;
                foreach (var td in tds)
                {
                    var className = await page.EvaluateFunctionAsync<string>("el => el.className", td);
                    if (className.Contains("gav-pom-key"))
                    {
                        key = await(await td.GetPropertyAsync("textContent")).JsonValueAsync<string>();
                    }
                    else
                    {
                        // only include link- or text-based metadata
                        var datas = (await td.QuerySelectorAllAsync("a")).IsEmptyEnumerable() ? await td.QuerySelectorAllAsync("span") :
                            await td.QuerySelectorAllAsync("a");

                        foreach (var data in datas)
                        {
                            if (data != null)
                            {
                                var rawValue = await(await data.GetPropertyAsync("textContent") ?? await data.GetPropertyAsync("href"))
                                    .JsonValueAsync<string>();

                                // format: remove newlines and excessive spaces and punctuation
                                rawValue = Regex.Replace(rawValue, @"\s{2,}", "");
                                rawValue = Regex.Replace(rawValue, @"\r\n|,", "");
                                rawValue = Regex.Replace(rawValue, @"\r\n|\n", "");

                                // append available artifact types as comma-separated string
                                if (value == string.Empty)
                                {
                                    value = rawValue;
                                }
                                else
                                {
                                    value += $", {rawValue}";
                                }
                            }
                        }
                    }
                }
                metadata.Add(key, value);
            }

            await browser.CloseAsync();
            return metadata;
        }


        public enum MavenArtifactType
        {
            Unknown = 0,
            Jar,
            JavadocJar,
            Pom,
            SourcesJar,
            TestsJar,
            TestSourcesJar,
            Aar,
        }

        private static MavenArtifactType GetMavenArtifactType(string fileName)
        {
            switch (fileName)
            {
                case string _ when fileName.EndsWith("-tests-sources.jar"): return MavenArtifactType.TestSourcesJar;
                case string _ when fileName.EndsWith("-tests.jar"): return MavenArtifactType.TestsJar;
                case string _ when fileName.EndsWith("-sources.jar"): return MavenArtifactType.SourcesJar;
                case string _ when fileName.EndsWith(".pom"): return MavenArtifactType.Pom;
                case string _ when fileName.EndsWith("-javadoc.jar"): return MavenArtifactType.JavadocJar;
                case string _ when fileName.EndsWith(".jar"): return MavenArtifactType.Jar;
                case string _ when fileName.EndsWith(".aar"): return MavenArtifactType.Aar;
                default: return MavenArtifactType.Unknown;
            }
        }

        public async Task<DateTime?> GetPackagePublishDateAsync(PackageURL purl, bool useCache = true)
        {
            string? packageName = Check.NotNull(nameof(purl.Name), purl?.Name);
            string? packageVersion = Check.NotNull(nameof(purl.Version), purl?.Version);
            string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT.GetRepositoryUrl();
            MavenSupportedUpstream upstream = feedUrl.GetMavenSupportedUpstream();

            HttpClient httpClient = CreateHttpClient();

            if (upstream == MavenSupportedUpstream.MavenCentralRepository)
            {
                string? packageNamespace = Check.NotNull(nameof(purl.Namespace), purl?.Namespace).Replace('.', '/');
                var baseUrl = $"{upstream.GetRepositoryUrl().EnsureTrailingSlash()}{packageNamespace}/{packageName}/";

                string? html = await GetHttpStringCache(httpClient, baseUrl, useCache);
                if (string.IsNullOrEmpty(html))
                {
                    throw new InvalidOperationException();
                }

                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);

                // Break the version content down into its individual lines, and then find the one that represents the
                // intended version.
                string? versionContent = document.QuerySelector("#contents").TextContent
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .SingleOrDefault(versionText => versionText.StartsWith($"{packageVersion}/"));

                if (versionContent == null)
                {
                    return null;
                }

                // Split the version content into its individual parts.
                // [0] - The version
                // [1] - The date it was published
                // [2] - The time it waas published
                // [3] - The download count
                string[] versionParts = versionContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (DateTime.TryParse($"{versionParts[1]} {versionParts[2]}", out DateTime publishDateTime))
                {
                    return publishDateTime;
                }
            }
            else if (upstream == MavenSupportedUpstream.GoogleMavenRepository)
            {
                string packageNamespace = purl.Namespace;
                var packageVersionUrl = $"{upstream.GetRepositoryUrl()}{packageNamespace}:{packageName}:{packageVersion}";

                // Google Maven Repository's webpage has dynamic content
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();
                var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();
                await page.GoToAsync(packageVersionUrl);

                // Google Maven Repository offers a "Last Updated Date", which will be considered as the publish timestamp.
                await page.WaitForSelectorAsync("tr");
                var trElement = await page.XPathAsync("//tr[td[contains(text(), 'Last Updated Date')]]");
                var content = await trElement.ElementAt(0).QuerySelectorAsync("span");
                var lastUpdatedDate = await content.EvaluateFunctionAsync<string>("el => el.textContent");

                await browser.CloseAsync();

                if (DateTime.TryParse($"{lastUpdatedDate}", out DateTime publishDateTime))
                {
                    return publishDateTime;
                }
            }

            return null;
        }
    }
}