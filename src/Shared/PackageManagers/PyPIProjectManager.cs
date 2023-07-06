// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Parser;
    using Contracts;
    using Extensions;
    using Helpers;
    using Model;
    using PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Utilities;
    using Version = SemanticVersioning.Version;

    public class PyPIProjectManager : TypedManager<IManagerPackageVersionMetadata, PyPIProjectManager.PyPIArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "pypi";

        public override string ManagerType => Type;

        public const string DEFAULT_PYPI_ENDPOINT = "https://pypi.org";
        public string ENV_PYPI_ENDPOINT { get; set; } = DEFAULT_PYPI_ENDPOINT;

        public PyPIProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory)
        {
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ArtifactUri<PyPIArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content))
            {
                throw new InvalidOperationException();
            }

            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            JsonElement.ArrayEnumerator? urlsArray = OssUtilities.GetJSONEnumerator(root.GetProperty("urls"));
            if (urlsArray is not null)
            {
                foreach (JsonElement url in urlsArray.Value)
                {
                    string? urlStr = OssUtilities.GetJSONPropertyStringIfExists(url, "url");
                    string? uploadTimeStr = OssUtilities.GetJSONPropertyStringIfExists(url, "upload_time");
                    DateTime uploadTime = DateTime.Parse(uploadTimeStr);

                    if (OssUtilities.GetJSONPropertyStringIfExists(url, "packagetype") == "sdist")
                    {
                        yield return new ArtifactUri<PyPIArtifactType>(PyPIArtifactType.Tarball, urlStr, uploadTime);
                    }

                    if (OssUtilities.GetJSONPropertyStringIfExists(url, "packagetype") == "bdist_wheel")
                    {
                        yield return new ArtifactUri<PyPIArtifactType>(PyPIArtifactType.Wheel, urlStr, uploadTime);
                    }
                }
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true)
        {
            Check.NotNull(nameof(owner), owner);
            HttpClient httpClient = CreateHttpClient();

            string? html = await GetHttpStringCache(httpClient, $"{ENV_PYPI_ENDPOINT}/user/{owner}", useCache);
            if (string.IsNullOrEmpty(html))
            {
                throw new InvalidOperationException();
            }

            HtmlParser parser = new();
            AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);
            foreach (AngleSharp.Dom.IElement packageSnippet in document.QuerySelectorAll("a.package-snippet"))
            {
                IElement? name = packageSnippet.FirstElementChild;
                yield return new PackageURL(Type, name.Text());
            }
        }

        /// <summary>
        /// Download one PyPI package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>the path or file written.</returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
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

            try
            {
                HttpClient httpClient = CreateHttpClient();

                JsonDocument? doc = await GetJsonCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json");

                if (!doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    return downloadedPaths;
                }

                foreach (JsonProperty versionObject in releases.EnumerateObject())
                {
                    if (versionObject.Name != packageVersion)
                    {
                        continue;
                    }
                    foreach (JsonElement release in versionObject.Value.EnumerateArray())
                    {
                        if (!release.TryGetProperty("packagetype", out JsonElement packageType))
                        {
                            continue;   // Missing a package type
                        }

                        System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(release.GetProperty("url").GetString());
                        result.EnsureSuccessStatusCode();
                        string targetName = $"pypi-{packageType}-{packageName}@{packageVersion}";
                        string extension = ".tar.gz";
                        if (packageType.ToString() == "bdist_wheel")
                        {
                            extension = ".whl";
                        }
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
                            extractionPath += extension;
                            await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                            downloadedPaths.Add(extractionPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading PyPI package: {0}", ex.Message);
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
            HttpClient httpClient = CreateHttpClient();

            return await CheckHttpCacheForPackage(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json", useCache);
        }

        /// <inheritdoc />
        public override async Task<bool> PackageVersionExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageVersionExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }

            if (purl.Version.IsBlank())
            {
                Logger.Trace("Provided PackageURL version was null or blank.");
                return false;
            }

            HttpClient httpClient = CreateHttpClient();
            string endpoint = $"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/{purl.Version}/json";

            return await CheckHttpCacheForPackage(httpClient, endpoint, useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();

                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json", useCache);
                List<string> versionList = new();
                if (doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    foreach (JsonProperty versionObject in releases.EnumerateObject())
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, versionObject.Name);
                        versionList.Add(versionObject.Name);
                    }
                }

                // Add the current version (not included in releases)
                if (doc.RootElement.TryGetProperty("info", out JsonElement info) &&
                    info.TryGetProperty("version", out JsonElement version))
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, version.GetString());
                    if (version.GetString() is string versionString && !string.IsNullOrWhiteSpace(versionString))
                    {
                        versionList.Add(versionString);
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

        /// <summary>
        /// Gets the <see cref="DateTime"/> a package version was published at.
        /// </summary>
        /// <param name="purl">Package URL specifying the package. Version is mandatory.</param>
        /// <param name="useCache">If the cache should be used when looking for the published time.</param>
        /// <returns>The <see cref="DateTime"/> when this version was published, or null if not found.</returns>
        public async Task<DateTime?> GetPublishedAtAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            DateTime? uploadTime = (await this.GetPackageMetadataAsync(purl, useCache))?.UploadTime;
            return uploadTime;
        }

        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                HttpClient httpClient = CreateHttpClient();

                if (purl.Version.IsNotBlank())
                {
                    return await GetHttpStringCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/{purl.Version}/json", useCache);
                }

                return await GetHttpStringCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/json", useCache);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching PyPI metadata: {0}", ex.Message);
                return null;
            }
        }

        /// <inheritdoc />
        /// <remarks>Currently doesn't respect the <paramref name="includePrerelease"/> flag.</remarks>
        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true)
        {
            PackageMetadata metadata = new();
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return null; }

            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            JsonElement infoElement = root.GetProperty("info");

            metadata.LatestPackageVersion = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "version"); // Ran in the root, always points to latest version.

            if (purl.Version.IsBlank() && metadata.LatestPackageVersion.IsNotBlank())
            {
                content = await GetMetadataAsync(purl.WithVersion(metadata.LatestPackageVersion), useCache);
                contentJSON = JsonDocument.Parse(content);
                root = contentJSON.RootElement;

                infoElement = root.GetProperty("info");
            }

            metadata.Name = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "name");
            metadata.Description = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "summary"); // Summary is the short description. Description is usually the readme.

            metadata.PackageManagerUri = ENV_PYPI_ENDPOINT;
            metadata.PackageUri = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "package_url");
            metadata.Keywords = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(infoElement, "keywords"));

            // author
            User author = new()
            {
                Name = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "author"),
                Email = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "author_email"),
            };
            metadata.Authors ??= new List<Model.User>();
            metadata.Authors.Add(author);

            // maintainers
            User maintainer = new()
            {
                Name = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "maintainer"),
                Email = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "maintainer_email"),
            };
            metadata.Maintainers ??= new List<User>();
            metadata.Maintainers.Add(maintainer);

            // repository
            Dictionary<PackageURL, double>? repoMappings = await SearchRepoUrlsInPackageMetadata(purl, content);
            foreach (KeyValuePair<PackageURL, double> repoMapping in repoMappings)
            {
                Repository repository = new()
                {
                    Rank = repoMapping.Value,
                    Type = repoMapping.Key.Type
                };
                await repository.ExtractRepositoryMetadata(repoMapping.Key);

                metadata.Repository ??= new List<Repository>();
                metadata.Repository.Add(repository);
            }

            // license
            string? licenseType = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "license");
            if (!string.IsNullOrWhiteSpace(licenseType))
            {
                metadata.Licenses ??= new List<License>();
                metadata.Licenses.Add(new License()
                {
                    Name = licenseType
                });
            }

            // get the version, either use the provided one, or if null then use the LatestPackageVersion.
            metadata.PackageVersion = purl.Version ?? metadata.LatestPackageVersion;

            // if we found any version at all, get the information.
            if (metadata.PackageVersion is not null)
            {
                JsonElement.ArrayEnumerator? urlsArray = OssUtilities.GetJSONEnumerator(root.GetProperty("urls"));
                if (urlsArray is not null)
                {
                    foreach (JsonElement url in urlsArray.Value)
                    {
                        // digests
                        if (OssUtilities.GetJSONPropertyIfExists(url, "digests")?.EnumerateObject()
                            is JsonElement.ObjectEnumerator digests)
                        {
                            metadata.Signature ??= new List<Digest>();
                            foreach (JsonProperty digest in digests)
                            {
                                metadata.Signature.Add(new Digest()
                                {
                                    Algorithm = digest.Name,
                                    Signature = digest.Value.ToString()
                                });
                            }
                        }

                        // TODO: Want to figure out how to store info for .whl files as well.
                        if (OssUtilities.GetJSONPropertyStringIfExists(url, "packagetype") == "sdist")
                        {
                            // downloads
                            if (OssUtilities.GetJSONPropertyIfExists(url, "downloads")?.GetInt64() is long downloads
                                && downloads != -1)
                            {
                                metadata.Downloads ??= new Downloads()
                                {
                                    Overall = downloads
                                };
                            }

                            metadata.Size = OssUtilities.GetJSONPropertyIfExists(url, "size")?.GetInt64();
                            metadata.Active = !OssUtilities.GetJSONPropertyIfExists(url, "yanked")?.GetBoolean();
                            metadata.VersionUri = $"{ENV_PYPI_ENDPOINT}/project/{purl.Name}/{purl.Version}";
                            metadata.VersionDownloadUri = OssUtilities.GetJSONPropertyStringIfExists(url, "url");
                        }

                        string? uploadTime = OssUtilities.GetJSONPropertyStringIfExists(url, "upload_time");
                        if (uploadTime != null)
                        {
                            DateTime newUploadTime = DateTime.Parse(uploadTime);
                            // Used to set the minimum upload time for all associated files for this version to get the publish time.
                            if (metadata.UploadTime == null || metadata.UploadTime > newUploadTime)
                            {
                                metadata.UploadTime = newUploadTime;
                            }
                        }
                    }
                }
            }

            return metadata;
        }

        public override async Task<DateTime?> GetPublishedAtUtcAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            HttpClient client = CreateHttpClient();

            JsonDocument contentJSON = await GetJsonCache(client, $"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/{purl.Version}/json");
            JsonElement root = contentJSON.RootElement;

            JsonElement.ArrayEnumerator? urlsArray = OssUtilities.GetJSONEnumerator(root.GetProperty("urls"));
            DateTime? uploadTime = null;
            if (urlsArray is not null)
            {
                foreach (JsonElement url in urlsArray.Value)
                {
                    string? urlStr = OssUtilities.GetJSONPropertyStringIfExists(url, "url");
                    string? uploadTimeStr = OssUtilities.GetJSONPropertyStringIfExists(url, "upload_time");
                    if (uploadTimeStr != null)
                    {
                        DateTime newUploadTime = DateTime.Parse(uploadTimeStr).ToUniversalTime();
                        // Used to set the minimum upload time for all associated files for this version to get the publish time.
                        if (uploadTime == null || uploadTime > newUploadTime)
                        {
                            uploadTime = newUploadTime;
                        }
                    }
                }
            }

            return uploadTime;
        }

        public override List<Version> GetVersions(JsonDocument? contentJSON)
        {
            List<Version> allVersions = new();
            if (contentJSON is null) { return allVersions; }

            JsonElement root = contentJSON.RootElement;
            try
            {
                JsonElement versions = root.GetProperty("releases");
                foreach (JsonProperty version in versions.EnumerateObject())
                {
                    // TODO: Fails if not a valid semver. ex 0.2 https://github.com/microsoft/OSSGadget/issues/328
                    allVersions.Add(new Version(version.Name));
                }
            }
            catch (KeyNotFoundException) { return allVersions; }
            catch (InvalidOperationException) { return allVersions; }

            return allVersions;
        }

        public override JsonElement? GetVersionElement(JsonDocument contentJSON, Version version)
        {
            try
            {
                JsonElement versionElement = contentJSON.RootElement.GetProperty("releases").GetProperty(version.ToString());
                return versionElement;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_PYPI_ENDPOINT}/project/{purl?.Name}");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Dictionary<PackageURL, double> mapping = new();
            if (purl.Name?.StartsWith('_') ?? false) // TODO: there are internal modules which do not start with _
            {
                // TODO: internal modules could also be in https://github.com/python/cpython/tree/master/Modules/
                mapping.Add(new PackageURL(purl.Type, purl.Namespace, purl.Name, null, null, "cpython/tree/master/Lib/"), 1.0F);
                return mapping;
            }
            if (string.IsNullOrEmpty(metadata))
            {
                return mapping;
            }
            JsonDocument contentJSON = JsonDocument.Parse(metadata);

            List<string> possibleLocationProperties = new() { "home_page", "download_url" };

            JsonElement infoJSON;
            try
            {
                infoJSON = contentJSON.RootElement.GetProperty("info");
            }
            catch (Exception)
            {
                return mapping;
            }

            foreach (JsonProperty property in infoJSON.EnumerateObject())
            {
                if (possibleLocationProperties.Contains(property.Name.ToLower()) && property.Value.ToString() is {} url)
                {
                    IEnumerable<PackageURL>? packageUrls = GitHubProjectManager.ExtractGitHubPackageURLs(url);
                    // if we were not able to extract a github url, continue
                    if (packageUrls.FirstOrDefault() is {} packageUrl)
                    {
                        mapping.Add(packageUrl, 1.0F);
                        return mapping;
                    }
                }

                try
                {
                    // See https://setuptools.pypa.io/en/latest/references/keywords.html
                    // project_urls is a map of arbitrary strings to strings (values are expected to be urls)
                    // The key name is arbitrary, so we can check all the keys and if any are detected as repository urls,
                    //   that should be what we are looking for.
                    if (property.Name.Equals("project_urls"))
                    {
                        foreach (JsonProperty projectUrl in property.Value.EnumerateObject())
                        {
                            if (projectUrl.Value.ToString() is { } possibleGitHubUrl)
                            {
                                IEnumerable<PackageURL>? packageUrls =
                                    GitHubProjectManager.ExtractGitHubPackageURLs(possibleGitHubUrl);
                                // if we were able to extract a github url, return
                                if (packageUrls.FirstOrDefault() is { } packageUrl)
                                {
                                    mapping.Add(packageUrl, 1.0F);
                                    return mapping;
                                }
                            }
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return mapping;
        }

        public enum PyPIArtifactType
        {
            Unknown = 0,
            Tarball,
            Wheel,
        }
    }
}