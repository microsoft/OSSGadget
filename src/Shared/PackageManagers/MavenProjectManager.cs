// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using AngleSharp.Html.Parser;
    using Helpers;
    using Microsoft.CST.OpenSource.Contracts;
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.Model.Enums;
    using Microsoft.CST.OpenSource.PackageActions;
    using Newtonsoft.Json.Linq;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Xml;

    public class MavenProjectManager : TypedManager<IManagerPackageVersionMetadata, MavenArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "maven";

        public override string ManagerType => Type;

        public const string DEFAULT_MAVEN_ENDPOINT = "https://repo1.maven.org/maven2";
        public const string GOOGLE_MAVEN_ENDPOINT = "https://maven.google.com";
        public string ENV_MAVEN_ENDPOINT { get; set; } = DEFAULT_MAVEN_ENDPOINT;

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
            string? packageNamespace = Check.NotNull(nameof(purl.Namespace), purl?.Namespace).Replace('.', '/');
            string? packageVersion = Check.NotNull(nameof(purl.Version), purl?.Version);
            string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT;

            var baseUrl = $"{feedUrl.EnsureTrailingSlash()}{packageNamespace}/{packageName}/{packageVersion}/";
            List<string>? fileNamesList = null;

            // Most Maven repositories have web server directory indices (but not Google Maven).
            // First attempt to obtain artifact download URIs via the index.
            if (feedUrl != GOOGLE_MAVEN_ENDPOINT)
            {
                fileNamesList = await GetArtifactDownloadUris_DirectoryIndexStrategyAsync(baseUrl, purl, useCache);
            }

            // Attempt to retrieve the artifact-metadata.json file (only new versions have one).
            fileNamesList ??= await GetArtifactDownloadUris_ArtifactMetadataStrategyAsync(baseUrl, purl, useCache);

            // Resort to manual file probe.
            fileNamesList ??= await GetArtifactDownloadUris_FileProbeStrategyAsync($"{baseUrl}{purl.Name}-{purl.Version}", purl, useCache);

            foreach (string fileName in fileNamesList)
            {
                MavenArtifactType artifactType = GetMavenArtifactType(fileName);
                yield return new ArtifactUri<MavenArtifactType>(artifactType, fileName);
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
            string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT;

            HttpClient httpClient = CreateHttpClient();

            string packageNamespace = purl.Namespace.Replace('.', '/');
            var baseUrl = $"{feedUrl.EnsureTrailingSlash()}{packageNamespace}/{packageName}/maven-metadata.xml";

            return await CheckHttpCacheForPackage(httpClient, baseUrl, useCache);
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
                string packageNamespace = purl.Namespace.Replace('.', '/');
                string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT;

                HttpClient httpClient = CreateHttpClient();

                var baseUrl = $"{feedUrl.EnsureTrailingSlash()}{packageNamespace}/{packageName}";

                List<string>? versionsList = null;

                // First try retrieving versions from the maven-metadata.xml.
                versionsList = await GetVersionsList_MavenMetadataStrategyAsync($"{baseUrl}/maven-metadata.xml", purl, useCache);

                // If maven-metadata.xml file is unavailable, try using the web server directory index.
                versionsList ??= await GetVersionsList_DirectoryIndexStrategyAsync(baseUrl, purl, useCache);

                return SortVersions(versionsList != null ? versionsList.Distinct() : new List<string>());
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
            if (purl is null || purl.Name is null || purl.Namespace is null || purl.Version is null)
            {
                return false;
            }
            try
            {
                string packageName = purl.Name;
                string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT;

                HttpClient httpClient = CreateHttpClient();

                string packageNamespace = purl.Namespace.Replace('.', '/');
                var baseUrl = $"{feedUrl.EnsureTrailingSlash()}{packageNamespace}/{packageName}/{purl.Version}/{packageName}-{purl.Version}.pom";

                string? content = await GetHttpStringCache(httpClient, baseUrl, useCache);
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
                string? packageNamespace = purl?.Namespace?.Replace('.', '/');
                string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT;

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

                var baseUrl = $"{feedUrl.EnsureTrailingSlash()}{packageNamespace}/{packageName}/{purl.Version}/{packageName}-{purl.Version}.pom";
                return await GetHttpStringCache(httpClient, baseUrl, useCache);
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
            metadata.PackageManagerUri = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();
            metadata.Platform = "Maven";
            metadata.Language = "Java";

            metadata.UploadTime = await GetPackagePublishDateAsync(purl, useCache);

            return metadata;
        }

        private static MavenArtifactType GetMavenArtifactType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return MavenArtifactType.Unknown;
            }

            foreach (MavenArtifactType artifactType in Enum.GetValues<MavenArtifactType>())
            {
                if (fileName.EndsWith(artifactType.GetTypeNameExtension()))
                {
                    return artifactType;
                }
            }
            return MavenArtifactType.Unknown;
        }

        public async Task<DateTime?> GetPackagePublishDateAsync(PackageURL purl, bool useCache = true)
        {
            string? packageName = Check.NotNull(nameof(purl.Name), purl?.Name);
            string? packageNamespace = Check.NotNull(nameof(purl.Namespace), purl?.Namespace).Replace('.', '/');
            string? packageVersion = Check.NotNull(nameof(purl.Version), purl?.Version);
            string feedUrl = purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT;

            HttpClient httpClient = CreateHttpClient();
            var baseUrl = $"{feedUrl.EnsureTrailingSlash()}{packageNamespace}/{packageName}/";
            string? html = string.Empty;

            try
            {
                html = await GetHttpStringCache(httpClient, baseUrl, useCache);
            }
            catch (HttpRequestException) { }

            // Retrieve publish date via web server directory indices if available.
            if (!string.IsNullOrEmpty(html))
            {
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
                // [2] - The time it was published
                // [3] - The file size in bytes
                string[] versionParts = versionContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (DateTime.TryParse($"{versionParts[1]} {versionParts[2]}", out DateTime publishDateTime))
                {
                    return publishDateTime;
                }
            }

            // If the directory index approach does not work, try to get the "Last-Modified" header from the .pom file.
            baseUrl = $"{feedUrl.EnsureTrailingSlash()}{packageNamespace}/{packageName}/{packageVersion}/{packageName}-{packageVersion}.pom";
            HttpResponseMessage response = await httpClient.GetAsync(baseUrl);

            if (response.Content.Headers.TryGetValues("Last-Modified", out var values))
            {
                var lastModified = DateTime.Parse(values.First());
                return lastModified;
            }

            return null;
        }

        private async Task<List<string>>? GetArtifactDownloadUris_DirectoryIndexStrategyAsync(string baseUrl, PackageURL purl, bool useCache = true)
        {
            Logger.Trace($"Trying to retrieve artifact download URis for {purl} via directory index strategy.");

            HttpClient httpClient = CreateHttpClient();
            var fileNameList = new List<string>();

            try
            {
                string? html = await GetHttpStringCache(httpClient, baseUrl, useCache);
                if (string.IsNullOrEmpty(html))
                {
                    return null;
                }

                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);

                foreach (string fileName in document.QuerySelectorAll("a").Select(link => link.GetAttribute("href").ToString()))
                {
                    if (fileName == "../") continue;
                    fileNameList.Add(baseUrl + fileName);
                }
            }
            catch (Exception e)
            {
                Logger.Trace($"Directory index strategy for {purl} was unsuccessful: {e.Message}");
                return null;
            }

            return fileNameList;
        }

        private async Task<List<string>>? GetArtifactDownloadUris_ArtifactMetadataStrategyAsync(string baseUrl, PackageURL purl, bool useCache = true)
        {
            Logger.Trace($"Trying to retrieve artifact download URis for {purl} via artifact metadata strategy.");

            HttpClient httpClient = CreateHttpClient();
            var fileNameList = new List<string>();

            try
            {
                string? artifactMetadata = await GetHttpStringCache(httpClient, $"{baseUrl}artifact-metadata.json", useCache);

                // add all artifacts from the artifact-metadata.json
                JObject jsonObject = JObject.Parse(artifactMetadata);
                JArray artifactsArray = (JArray)jsonObject["artifacts"];

                foreach (JObject artifact in artifactsArray)
                {
                    string fileName = artifact["name"].ToString();
                    fileNameList.Add(baseUrl + fileName);
                }
            }
            catch (Exception e)
            {
                Logger.Trace($"Artifact metadata strategy for {purl} was unsuccessful: {e.Message}");
                return null;
            }

            return fileNameList;
        }

        private async Task<List<string>>? GetArtifactDownloadUris_FileProbeStrategyAsync(string baseUrl, PackageURL purl, bool useCache = true)
        {
            Logger.Trace($"Trying to retrieve artifact download URis for {purl} via file probe strategy.");

            HttpClient httpClient = CreateHttpClient();
            var fileNamesList = new List<string>();

            foreach (MavenArtifactType artifactType in Enum.GetValues<MavenArtifactType>())
            {
                if (artifactType != MavenArtifactType.Unknown)
                {
                    try
                    {
                        var extension = artifactType.GetTypeNameExtension();
                        var fileName = $"{baseUrl}{extension}";
                        string? artifactMetadata = await GetHttpStringCache(httpClient, fileName, useCache);
                        fileNamesList.Add(fileName);
                    }
                    catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // file type does not exist
                    }
                    catch (Exception e)
                    {
                        Logger.Trace($"Unexpected error during file probe strategy for {purl}: {e.Message}");
                    }
                }
            }

            return fileNamesList;
        }

        private async Task<List<string>>? GetVersionsList_MavenMetadataStrategyAsync(string baseUrl, PackageURL purl, bool useCache)
        {
            Logger.Trace($"Trying to retrieve versions list for {purl} via maven-metadata.xml strategy.");

            HttpClient httpClient = CreateHttpClient();
            var versionsList = new List<string>();

            try
            {
                string? content = await GetHttpStringCache(httpClient, baseUrl, useCache);

                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }

                // Parse the maven-metadata.xml file for the versions.
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(content);

                var packageElement = xmlDoc.SelectSingleNode($"//versions");

                if (packageElement != null)
                {
                    foreach (XmlNode versionNode in packageElement.ChildNodes)
                    {
                        versionsList.Add(versionNode.InnerText.Trim());
                    }
                }
            }
            catch (Exception e)
            {

                Logger.Trace($"Maven-metadata strategy for {purl} versioning was unsuccessful: {e.Message}");
                return null;
            }

            return versionsList;
        }

        private async Task<List<string>>? GetVersionsList_DirectoryIndexStrategyAsync(string baseUrl, PackageURL purl, bool useCache)
        {
            Logger.Trace($"Trying to retrieve versions list for {purl} via directory index strategy.");

            HttpClient httpClient = CreateHttpClient();
            var versionsList = new List<string>();

            try
            {
                string? content = await GetHttpStringCache(httpClient, baseUrl, useCache);

                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }

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
                        Logger.Debug("Identified {0} version {1}.", purl.Name, versionStr);
                        versionsList.Add(versionStr);
                    }
                }

                return versionsList;
            }
            catch (Exception e)
            {
                Logger.Trace($"Directory index strategy for {purl} versioning was unsuccessful: {e.Message}");
                return null;
            }
        }
    }
}