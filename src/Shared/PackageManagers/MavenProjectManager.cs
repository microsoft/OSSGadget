// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using AngleSharp.Html.Parser;
    using Helpers;
    using Microsoft.CST.OpenSource.Contracts;
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class MavenProjectManager : TypedManager<IManagerPackageVersionMetadata, MavenProjectManager.MavenArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "maven";

        public override string ManagerType => Type;

        public const string DEFAULT_MAVEN_ENDPOINT = "https://repo1.maven.org/maven2";
        public string ENV_MAVEN_ENDPOINT { get; set; } = DEFAULT_MAVEN_ENDPOINT;

        public MavenProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory)
        {
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ArtifactUri<MavenArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            string? packageNamespace = Check.NotNull(nameof(purl.Namespace), purl?.Namespace).Replace('.', '/');
            string? packageName = Check.NotNull(nameof(purl.Name), purl?.Name);
            string? packageVersion = Check.NotNull(nameof(purl.Version), purl?.Version);
            string feedUrl = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();

            HttpClient httpClient = CreateHttpClient();
            string baseUrl = $"{feedUrl}{packageNamespace}/{packageName}/{packageVersion}/";
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
                Logger.Warn("Unable to download [{0} {1} {2}]. Both must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            IEnumerable<ArtifactUri<MavenArtifactType>> artifacts = (await GetArtifactDownloadUrisAsync(purl, useCache: cached).ToListAsync())
                .Where(artifact => artifact.Type is MavenArtifactType.Jar or MavenArtifactType.SourcesJar or MavenArtifactType.JavadocJar);
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
            string packageNamespace = purl.Namespace.Replace('.', '/');
            string packageName = purl.Name;
            string feedUrl = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();
            HttpClient httpClient = CreateHttpClient();

            return await CheckHttpCacheForPackage(httpClient, $"{feedUrl}{packageNamespace}/{packageName}/maven-metadata.xml", useCache);
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
                string packageNamespace = purl.Namespace.Replace('.', '/');
                string packageName = purl.Name;
                string feedUrl = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();
                HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, $"{feedUrl}{packageNamespace}/{packageName}/", useCache);
                List<string> versionList = new();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new List<string>();
                }
                
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
                string packageNamespace = purl.Namespace.Replace('.', '/');
                string packageName = purl.Name;
                string feedUrl = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();
                HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, $"{feedUrl}{packageNamespace}/{packageName}/{purl.Version}/", useCache);
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
                string? packageNamespace = purl?.Namespace?.Replace('.', '/');
                string? packageName = purl?.Name;
                string feedUrl = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();
                HttpClient httpClient = CreateHttpClient();
                if (purl?.Version == null)
                {
                    foreach (string? version in await EnumerateVersionsAsync(purl, useCache))
                    {
                        return await GetHttpStringCache(httpClient, $"{feedUrl}{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom", useCache);
                    }
                    throw new Exception("No version specified and unable to enumerate.");
                }
                else
                {
                    string version = purl.Version;
                    return await GetHttpStringCache(httpClient, $"{feedUrl}{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom", useCache);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Error fetching Maven metadata: {ex.Message}");
                return null;
            }
        }

        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true)
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

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageNamespace = purl?.Namespace?.Replace('.', '/');
            string? packageName = purl?.Name;
            string feedUrl = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();

            return new Uri($"{feedUrl}{packageNamespace}/{packageName}");
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
                default: return MavenArtifactType.Unknown;
            }
        }

        private async Task<DateTime?> GetPackagePublishDateAsync(PackageURL purl, bool useCache = true)
        {
            string? packageNamespace = Check.NotNull(nameof(purl.Namespace), purl?.Namespace).Replace('.', '/');
            string? packageName = Check.NotNull(nameof(purl.Name), purl?.Name);
            string? packageVersion = Check.NotNull(nameof(purl.Version), purl?.Version);
            string feedUrl = (purl?.Qualifiers?["repository_url"] ?? ENV_MAVEN_ENDPOINT).EnsureTrailingSlash();

            HttpClient httpClient = CreateHttpClient();
            string baseUrl = $"{feedUrl}{packageNamespace}/{packageName}/";
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

            return null;
        }
    }
}