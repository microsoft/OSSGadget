// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Helpers;
    using Model;
    using NLog.LayoutRenderers.Wrappers;
    using PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

        public static string ENV_PYPI_ENDPOINT { get; set; } = "https://pypi.org";

        public PyPIProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory)
        {
        }
        
        /// <inheritdoc />
        public override IEnumerable<ArtifactUri<PyPIArtifactType>> GetArtifactDownloadUris(PackageURL purl)
        {
            string feedUrl = (purl.Qualifiers?["repository_url"] ?? ENV_PYPI_ENDPOINT).EnsureTrailingSlash();

            // Format: https://pypi.org/packages/source/{ package_name_first_letter }/{ package_name }/{ package_name }-{ package_version }.tar.gz
            string artifactUri =
                $"{feedUrl}packages/source/{char.ToLower(purl.Name[0])}/{purl.Name.ToLower()}/{purl.Name.ToLower()}-{purl.Version}.tar.gz";
            yield return new ArtifactUri<PyPIArtifactType>(PyPIArtifactType.Tarball, artifactUri);
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

            return await CheckJsonCacheForPackage(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json", useCache);
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
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                HttpClient httpClient = CreateHttpClient();

                return await GetHttpStringCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/json", useCache);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching PyPI metadata: {0}", ex.Message);
                return null;
            }
        }

        /// <inheritdoc />
        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool useCache = true)
        {
            PackageMetadata metadata = new();
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return null; }

            // convert NPM package data to normalized form
            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            JsonElement infoElement = root.GetProperty("info");

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

            // get the version
            List<Version> versions = GetVersions(contentJSON);
            Version? latestVersion = GetLatestVersion(versions);

            if (purl.Version != null)
            {
                // find the version object from the collection
                metadata.PackageVersion = purl.Version;
            }
            else
            {
                metadata.PackageVersion = latestVersion is null ? purl.Version : latestVersion?.ToString();
            }

            // if we found any version at all, get the information.
            if (metadata.PackageVersion is not null)
            {
                Version versionToGet = new(metadata.PackageVersion);
                JsonElement? versionElement = GetVersionElement(contentJSON, versionToGet);
                if (versionElement is not null)
                {
                    // fill the version specific entries

                    if (versionElement.Value.ValueKind == JsonValueKind.Array) // I think this should always be true.
                    {
                        foreach (JsonElement releaseFile in versionElement.Value.EnumerateArray())
                        {
                            // digests
                            if (OssUtilities.GetJSONPropertyIfExists(releaseFile, "digests")?.EnumerateObject()
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
                            if (OssUtilities.GetJSONPropertyStringIfExists(releaseFile, "packagetype") == "sdist")
                            {
                                // downloads
                                if (OssUtilities.GetJSONPropertyIfExists(releaseFile, "downloads")?.GetInt64() is long downloads
                                    && downloads != -1)
                                {
                                    metadata.Downloads ??= new Downloads()
                                    {
                                        Overall = downloads
                                    };
                                }

                                metadata.Size = OssUtilities.GetJSONPropertyIfExists(releaseFile, "size")?.GetInt64();
                                metadata.UploadTime = OssUtilities.GetJSONPropertyStringIfExists(releaseFile, "upload_time");
                                metadata.Active = !OssUtilities.GetJSONPropertyIfExists(releaseFile, "yanked")?.GetBoolean();
                                metadata.VersionUri = $"{ENV_PYPI_ENDPOINT}/project/{purl.Name}/{purl.Version}";
                                metadata.VersionDownloadUri = OssUtilities.GetJSONPropertyStringIfExists(releaseFile, "url");
                            }
                        }
                    }
                }
            }

            return metadata;
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

            List<string> possibleProperties = new() { "homepage", "home_page" };
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
            {   // there are a couple of possibilities where the repository url might be present - check all of them
                try
                {
                    if (possibleProperties.Contains(property.Name.ToLower()))
                    {
                        string homepage = property.Value.ToString() ?? string.Empty;
                        IEnumerable<PackageURL>? packageUrls = GitHubProjectManager.ExtractGitHubPackageURLs(homepage);
                        // if we were able to extract a github url, return
                        if (packageUrls != null && packageUrls.Any())
                        {
                            mapping.Add(packageUrls.First(), 1.0F);
                            return mapping;
                        }
                    }
                    else if (property.Name.Equals("project_urls"))
                    {
                        if (property.Value.TryGetProperty("Source",out JsonElement jsonElement))
                        {
                            string? sourceLoc = jsonElement.GetString();
                            if (sourceLoc != null)
                            {
                                IEnumerable<PackageURL>? packageUrls = GitHubProjectManager.ExtractGitHubPackageURLs(sourceLoc);
                                if (packageUrls != null && packageUrls.Any())
                                {
                                    mapping.Add(packageUrls.First(), 1.0F);
                                    return mapping;
                                }
                            }
                        }
                    }
                }
                catch (Exception) { continue; /* try the next property */ }
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