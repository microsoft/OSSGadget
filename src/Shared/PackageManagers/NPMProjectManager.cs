// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Extensions;
    using Helpers;
    using Microsoft.CST.OpenSource.Model;
    using Model.Enums;
    using Model.PackageExistence;
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

    public class NPMProjectManager : TypedManager<IManagerPackageVersionMetadata, NPMProjectManager.NPMArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "npm";

        public override string ManagerType => Type;

        public const string DEFAULT_NPM_API_ENDPOINT = "https://registry.npmjs.org";
        public string ENV_NPM_API_ENDPOINT { get; set; } = DEFAULT_NPM_API_ENDPOINT;
        
        public const string DEFAULT_ENV_NPM_ENDPOINT = "https://www.npmjs.com";
        public string ENV_NPM_ENDPOINT { get; set; } = DEFAULT_ENV_NPM_ENDPOINT;

        // Should this be overridable by Environment Helper?
        private static readonly string NPM_SECURITY_HOLDING_VERSION = "0.0.1-security";

        public NPMProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory)
        {
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ArtifactUri<NPMArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            string feedUrl = (purl.Qualifiers?["repository_url"] ?? ENV_NPM_API_ENDPOINT).EnsureTrailingSlash();

            string artifactUri = purl.HasNamespace() ? 
                $"{feedUrl}{purl.GetNamespaceFormatted()}/{purl.Name}/-/{purl.Name}-{purl.Version}.tgz" : // If there's a namespace.
                $"{feedUrl}{purl.Name}/-/{purl.Name}-{purl.Version}.tgz"; // If there isn't a namespace.
            yield return new ArtifactUri<NPMArtifactType>(NPMArtifactType.Tarball, artifactUri);
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true)
        {
            Check.NotNull(nameof(owner), owner);
            HttpClient httpClient = CreateHttpClient();

            string? content = await GetHttpStringCache(httpClient, $"{ENV_NPM_API_ENDPOINT}/-/user/{owner}/package", useCache);
            if (string.IsNullOrEmpty(content))
            {
                throw new InvalidOperationException();
            }

            JsonElement root = JsonDocument.Parse(content).RootElement;
            foreach (JsonProperty package in root.EnumerateObject())
            {
                string name = package.Name.Replace("@", "%40");
                yield return new PackageURL($"pkg:{Type}/{name}");
            }
        }

        /// <summary>
        /// Download one NPM package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            // shouldn't happen here, but check
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                string uri = string.IsNullOrEmpty(purl?.Namespace)
                    ? $"{ENV_NPM_API_ENDPOINT}/{packageName}"
                    : $"{ENV_NPM_API_ENDPOINT}/{purl.Namespace}/{packageName}";
                JsonDocument doc = await GetJsonCache(httpClient, uri);
                string? tarball = doc.RootElement.GetProperty("versions").GetProperty(packageVersion).GetProperty("dist").GetProperty("tarball").GetString();
                HttpResponseMessage result = await httpClient.GetAsync(tarball);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl?.ToString());
                string targetName = $"npm-{packageName}@{packageVersion}";
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
                    extractionPath += Path.GetExtension(tarball) ?? "";
                    await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(extractionPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading NPM package: {0}", ex.Message);
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
            string packageName = purl.GetFullName();
            HttpClient httpClient = CreateHttpClient();

            return await CheckHttpCacheForPackage(httpClient, $"{ENV_NPM_API_ENDPOINT}/{packageName}", useCache);
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

            string packageName = purl.GetFullName();
            HttpClient httpClient = CreateHttpClient();
            string endpoint = $"{ENV_NPM_API_ENDPOINT}/{packageName}/{purl.Version}";

            return await CheckHttpCacheForPackage(httpClient, endpoint, useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl?.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string packageName = purl.GetFullName();
                string? latestVersion = null;

                string? content = await GetMetadataAsync(purl, useCache);
                if (string.IsNullOrEmpty(content)) { return new List<string>(); }

                JsonDocument contentJSON = JsonDocument.Parse(content);
                JsonElement root = contentJSON.RootElement;
                List<string> versionList = new();

                if (root.TryGetProperty("versions", out JsonElement versions))
                {
                    foreach (JsonProperty versionKey in versions.EnumerateObject())
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, versionKey.Name);
                        versionList.Add(versionKey.Name);
                    }
                }
                if (root.TryGetProperty("dist-tags", out JsonElement distTags) && distTags.TryGetProperty("latest", out JsonElement latestElement))
                {
                    latestVersion = latestElement.GetString();
                }

                // If there was no "latest" property for some reason.
                if (string.IsNullOrWhiteSpace(latestVersion))
                {
                    return SortVersions(versionList.Distinct());
                }

                Logger.Debug("Identified {0} latest version as {1}.", packageName, latestVersion);

                // Remove the latest version from the list of versions, so we can add it after sorting.
                versionList.Remove(latestVersion);
                
                // Sort the list of distinct versions.
                List<string> sortedList = SortVersions(versionList.Distinct()).ToList();
                
                // Insert the latest version at the beginning of the list.
                sortedList.Insert(0, latestVersion);

                return sortedList;
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
        /// Gets the latest version of the package
        /// </summary>
        /// <param name="contentJSON"></param>
        /// <returns></returns>
        public JsonElement? GetLatestVersionElement(JsonDocument contentJSON)
        {
            List<Version> versions = GetVersions(contentJSON);
            Version? maxVersion = GetLatestVersion(versions);
            if (maxVersion is null) { return null; }
            return GetVersionElement(contentJSON, maxVersion);
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                string? packageName = purl.HasNamespace() ? $"{purl.GetNamespaceFormatted()}/{purl.Name}" : purl.Name;
                HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, $"{ENV_NPM_API_ENDPOINT}/{packageName}", useCache);
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching NPM metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri(ENV_NPM_API_ENDPOINT.EnsureTrailingSlash() + (purl.HasNamespace() ? $"{purl.GetNamespaceFormatted()}/{purl.Name}" : purl.Name));
        }

        /// <inheritdoc />
        /// <remarks>Currently doesn't respect the <paramref name="includePrerelease"/> flag.</remarks>
        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true)
        {
            PackageMetadata metadata = new();
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return null; }

            // convert NPM package data to normalized form
            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            metadata.Name = root.GetProperty("name").GetString();
            metadata.Description = OssUtilities.GetJSONPropertyStringIfExists(root, "description");

            metadata.PackageManagerUri = ENV_NPM_ENDPOINT;
            metadata.Platform = "NPM";
            metadata.Language = "JavaScript";
            metadata.PackageUri = $"{metadata.PackageManagerUri}/package/{metadata.Name}";
            metadata.ApiPackageUri = $"{ENV_NPM_API_ENDPOINT}/{metadata.Name}";

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

            // if we found any version at all, get the information
            if (metadata.PackageVersion != null)
            {
                Version versionToGet = new(metadata.PackageVersion);
                JsonElement? versionElement = GetVersionElement(contentJSON, versionToGet);
                metadata.UploadTime = ParseUploadTime(contentJSON, metadata.PackageVersion);

                if (versionElement != null)
                {
                    // redo the generic values to version specific values
                    metadata.PackageUri = $"{ENV_NPM_ENDPOINT}/package/{metadata.Name}";
                    metadata.VersionUri = $"{ENV_NPM_ENDPOINT}/package/{metadata.Name}/v/{metadata.PackageVersion}";
                    metadata.ApiVersionUri = $"{ENV_NPM_API_ENDPOINT}/{metadata.Name}/{metadata.PackageVersion}";

                    // prioritize the version level description
                    if (OssUtilities.GetJSONPropertyStringIfExists(versionElement, "description") is string description)
                    {
                        metadata.Description = description;
                    }
                    
                    JsonElement? distElement = OssUtilities.GetJSONPropertyIfExists(versionElement, "dist");
                    if (OssUtilities.GetJSONPropertyIfExists(distElement, "tarball") is JsonElement tarballElement)
                    {
                        metadata.VersionDownloadUri = tarballElement.ToString().IsBlank() ?
                            $"{ENV_NPM_API_ENDPOINT}/{metadata.Name}/-/{metadata.Name}-{metadata.PackageVersion}.tgz"
                            : tarballElement.ToString();
                    }

                    if (OssUtilities.GetJSONPropertyIfExists(distElement, "integrity") is JsonElement integrityElement &&
                        integrityElement.ToString() is string integrity &&
                        integrity.Split('-') is string[] pair &&
                        pair.Length == 2)
                    {
                        metadata.Signature ??= new List<Digest>();
                        metadata.Signature.Add(new Digest()
                        {
                            Algorithm = pair[0],
                            Signature = pair[1]
                        });
                    }
                    
                    // size
                    if (OssUtilities.GetJSONPropertyIfExists(distElement, "unpackedSize") is JsonElement sizeElement &&
                        sizeElement.GetInt64() is long size)
                    {
                        metadata.Size = size;
                    }

                    // check for typescript
                    List<string>? devDependencies = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "devDependencies"));
                    if (devDependencies is not null && devDependencies.Count > 0 && devDependencies.Any(stringToCheck => stringToCheck.Contains("\"typescript\":")))
                    {
                        metadata.Language = "TypeScript";
                    }

                    // homepage
                    if (OssUtilities.GetJSONPropertyStringIfExists(versionElement, "homepage") is string homepage &&
                        !string.IsNullOrWhiteSpace(homepage))
                    {
                        metadata.Homepage = homepage;
                    }
                    
                    // commit id
                    if (OssUtilities.GetJSONPropertyStringIfExists(versionElement, "gitHead") is string gitHead &&
                        !string.IsNullOrWhiteSpace(gitHead))
                    {
                        metadata.CommitId = gitHead;
                    }

                    // install scripts
                    List<string>? scripts = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "scripts"));
                    if (scripts is not null && scripts.Count > 0)
                    {
                        metadata.Scripts ??= new List<Command>();
                        scripts.ForEach((element) => metadata.Scripts.Add(new Command { CommandLine = element }));
                    }

                    // dependencies
                    List<string>? dependencies = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "dependencies"));
                    if (dependencies is not null && dependencies.Count > 0)
                    {
                        metadata.Dependencies ??= new List<Dependency>();
                        dependencies.ForEach((dependency) => metadata.Dependencies.Add(new Dependency() { Package = dependency }));
                    }

                    // author(s)
                    JsonElement? authorElement = OssUtilities.GetJSONPropertyIfExists(versionElement, "_npmUser");
                    if (authorElement is not null)
                    {
                        User author = new()
                        {
                            Name = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "name"),
                            Email = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "email"),
                            Url = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "url")
                        };

                        metadata.Authors ??= new List<User>();
                        metadata.Authors.Add(author);
                    }

                    // maintainers
                    JsonElement? maintainersElement = OssUtilities.GetJSONPropertyIfExists(versionElement, "maintainers");
                    if (maintainersElement?.EnumerateArray() is JsonElement.ArrayEnumerator maintainerEnumerator)
                    {
                        metadata.Maintainers ??= new List<User>();
                        maintainerEnumerator.ToList().ForEach((element) =>
                        {
                            metadata.Maintainers.Add(
                                new User
                                {
                                    Name = OssUtilities.GetJSONPropertyStringIfExists(element, "name"),
                                    Email = OssUtilities.GetJSONPropertyStringIfExists(element, "email"),
                                    Url = OssUtilities.GetJSONPropertyStringIfExists(element, "url")
                                });
                        });
                    }

                    // repository
                    Dictionary<PackageURL, double> repoMappings = await SearchRepoUrlsInPackageMetadata(purl, content);
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

                    // keywords
                    metadata.Keywords = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "keywords"));

                    // licenses
                    {
                        if (OssUtilities.GetJSONEnumerator(OssUtilities.GetJSONPropertyIfExists(versionElement, "licenses"))
                                is JsonElement.ArrayEnumerator enumeratorElement &&
                            enumeratorElement.ToList() is List<JsonElement> enumerator &&
                            enumerator.Any())
                        {
                            metadata.Licenses ??= new List<License>();
                            // TODO: Convert/append SPIX_ID values?
                            enumerator.ForEach((license) =>
                            {
                                metadata.Licenses.Add(new License()
                                {
                                    Name = OssUtilities.GetJSONPropertyStringIfExists(license, "type"),
                                    Url = OssUtilities.GetJSONPropertyStringIfExists(license, "url")
                                });
                            });
                        }
                    }
                }
            }

            if (latestVersion is not null)
            {
                metadata.LatestPackageVersion = latestVersion.ToString();
            }

            return metadata;
        }

        public override async Task<DateTime?> GetPublishedAtUtcAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            HttpClient client = CreateHttpClient();
            string? packageName = purl.HasNamespace() ? $"{purl.GetNamespaceFormatted()}/{purl.Name}" : purl.Name;
            JsonDocument jsonDoc = await GetJsonCache(client, $"{ENV_NPM_API_ENDPOINT}/{packageName}", useCache);
            return ParseUploadTime(jsonDoc, purl.Version);
        }

        private DateTime? ParseUploadTime(JsonDocument jsonDoc, string versionKey)
        {
            if (jsonDoc.RootElement.TryGetProperty("time", out JsonElement time))
            {
                string? uploadTime = OssUtilities.GetJSONPropertyStringIfExists(time, versionKey);
                if (uploadTime != null)
                {
                    return DateTime.Parse(uploadTime).ToUniversalTime();
                }
            }
            return null;

        }

        public override JsonElement? GetVersionElement(JsonDocument? contentJSON, Version version)
        {
            if (contentJSON is null) { return null; }
            JsonElement root = contentJSON.RootElement;

            try
            {
                JsonElement versionsJSON = root.GetProperty("versions");
                foreach (JsonProperty versionProperty in versionsJSON.EnumerateObject())
                {
                    if (string.Equals(versionProperty.Name, version.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        return versionsJSON.GetProperty(version.ToString());
                    }
                }
            }
            catch (KeyNotFoundException) { return null; }
            catch (InvalidOperationException) { return null; }

            return null;
        }

        public override List<Version> GetVersions(JsonDocument? contentJSON)
        {
            List<Version> allVersions = new();
            if (contentJSON is null) { return allVersions; }

            JsonElement root = contentJSON.RootElement;
            try
            {
                JsonElement versions = root.GetProperty("versions");
                foreach (JsonProperty version in versions.EnumerateObject())
                {
                    allVersions.Add(new Version(version.Name));
                }
            }
            catch (KeyNotFoundException) { return allVersions; }
            catch (InvalidOperationException) { return allVersions; }

            return allVersions;
        }

        public override async Task<IPackageExistence> DetailedPackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("DetailedPackageExists {0}", purl?.ToString());
            if (purl is null)
            {
                Logger.Trace("Provided PackageURL was null.");
                throw new ArgumentNullException(nameof(purl), "Provided PackageURL was null.");
            }

            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return new PackageNotFound(); }

            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;
            
            HashSet<PackageRemovalReason> removalReasons = new();

            bool pulled = PackagePulled(root);
            bool consideredMalicious = PackageConsideredMalicious(root);

            if (pulled)
            {
                removalReasons.Add(PackageRemovalReason.PackageUnpublished);
            }

            if (consideredMalicious)
            {
                removalReasons.Add(PackageRemovalReason.RemovedByRepository);
            }

            if (pulled || consideredMalicious)
            {
                return new PackageRemoved(removalReasons);
            }

            return new PackageExists();
        }

        public override async Task<IPackageExistence> DetailedPackageVersionExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("DetailedPackageVersionExists {0}", purl?.ToString());
            if (purl is null)
            {
                Logger.Trace("Provided PackageURL was null.");
                throw new ArgumentNullException(nameof(purl), "Provided PackageURL was null.");
            }
            
            if (purl.Version.IsBlank())
            {
                Logger.Trace("Provided PackageURL version was blank.");
                throw new ArgumentNullException(nameof(purl.Version), 
                    "Cannot call DetailedPackageVersionExists on a purl without a version. Call DetailedPackageExists.");
            }

            bool packageVersionCurrentlyExists = await PackageVersionExistsAsync(purl, useCache);
            if (packageVersionCurrentlyExists) { return new PackageVersionExists(); }
            
            // if version isn't currently listed, check for other kinds of existence
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return new PackageNotFound(); }

            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            // Check to make sure that the package version ever existed.
            bool versionEverExisted = PackageVersionEverExisted(purl, root);
            if (!versionEverExisted)
            {
                return new PackageVersionNotFound();
            }
            
            HashSet<PackageVersionRemovalReason> removalReasons = new();

            bool pulled = PackagePulled(root);
            bool versionPulled = PackageVersionPulled(purl, root);
            bool consideredMalicious = PackageConsideredMalicious(root);

            if (pulled)
            {
                removalReasons.Add(PackageVersionRemovalReason.PackageUnpublished);
            }
            
            if (versionPulled)
            {
                removalReasons.Add(PackageVersionRemovalReason.VersionUnpublished);
            }

            // The version has to be pulled entirely in order for it to be considered malicious.
            if (!versionEverExisted && consideredMalicious)
            {
                removalReasons.Add(PackageVersionRemovalReason.RemovedByRepository);
            }

            if (pulled || versionPulled || (!versionEverExisted && consideredMalicious))
            {
                return new PackageVersionRemoved(removalReasons);
            }

            return new PackageVersionExists();
        }

        /// <summary>
        /// Check to see if the package was pulled from the repository.
        /// </summary>
        /// <param name="root">The <see cref="JsonElement"/> root content of the metadata.</param>
        /// <returns>True if this package was pulled. False otherwise.</returns>
        internal virtual bool PackagePulled(JsonElement root)
        {
            // If there is no version tag, then the entire package was unpublished.
            return !root.TryGetProperty("versions", out _);
        }

        /// <summary>
        /// Check to see if the package version was pulled from the repository.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> to check.</param>
        /// <param name="root">The <see cref="JsonElement"/> root content of the metadata.</param>
        /// <returns>True if this package version was pulled. False otherwise.</returns>
        internal virtual bool PackageVersionPulled(PackageURL purl, JsonElement root)
        {
            bool unpublishedFlag = false;
            bool unpublishedFromVersionDict = false;

            if (root.TryGetProperty("time", out JsonElement time))
            {
                // Example: https://registry.npmjs.org/@somosme/webflowutils
                if (time.TryGetProperty("unpublished", out JsonElement unpublishedElement))
                {
                    List<string>? versions = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(unpublishedElement, "versions"));
                    unpublishedFlag = versions?.Contains(purl.Version) ?? false;
                }
                
                // Alternatively sometimes the version gets pulled and doesn't show it in "unpublished".
                // So if there is a time entry for the version, but no entry in the dictionary of versions, then it was unpublished.
                // Example: https://registry.npmjs.org/%40achievementify/client version 0.2.1
                JsonElement? packageVersionTime = OssUtilities.GetJSONPropertyIfExists(time, purl.Version);
                if (packageVersionTime != null && !unpublishedFlag)
                {
                    unpublishedFromVersionDict = !root
                        .GetProperty("versions")
                        .EnumerateObject()
                        .Any(version => version.Name.Equals(purl.Version));
                }
            }
            
            return unpublishedFlag || unpublishedFromVersionDict;
        }
        
        /// <summary>
        /// Check to see if the package has a NPM security holding package.
        /// </summary>
        /// <param name="root">The <see cref="JsonElement"/> root content of the metadata.</param>
        /// <returns>True if this package is a NPM security holding package. False otherwise.</returns>
        internal virtual bool PackageConsideredMalicious(JsonElement root)
        {
            JsonElement time = root.GetProperty("time");
            return time.EnumerateObject().Any(timeEntry => timeEntry.Name == NPM_SECURITY_HOLDING_VERSION);
        }
        
        /// <summary>
        /// Check to see if the package version ever existed.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> to check.</param>
        /// <param name="root">The <see cref="JsonElement"/> root content of the metadata.</param>
        /// <returns>True if this package version ever existed. False otherwise.</returns>
        internal virtual bool PackageVersionEverExisted(PackageURL purl, JsonElement root)
        {
            // Did this version ever exist? Start with assuming not.
            bool everExisted = false;

            JsonElement time = root.GetProperty("time");
            
            // Check the unpublished property in time if it exists for the purl's version.
            if (time.TryGetProperty("unpublished", out JsonElement unpublishedElement))
            {
                List<string>? versions = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(unpublishedElement, "versions"));
                everExisted = versions?.Contains(purl.Version) ?? false;
            }

            // If the version wasn't in unpublished, then check if any of the versions in time match.
            if (!everExisted)
            {
                everExisted = time.EnumerateObject().Any(timeEntry => timeEntry.Name == purl.Version);
            }

            return everExisted;
        }

        /// <summary>
        /// Searches the package manager metadata to figure out the source code repository
        /// </summary>
        /// <param name="purl">the package for which we need to find the source code repository</param>
        /// <returns>
        /// A dictionary, mapping each possible repo source entry to its probability/empty dictionary
        /// </returns>
        protected override async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl,
            string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
            {
                return new Dictionary<PackageURL, double>();
            }
            JsonDocument contentJSON = JsonDocument.Parse(metadata);
            return await SearchRepoUrlsInPackageMetadata(purl, contentJSON);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl,
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            JsonDocument contentJSON)
        {
            Dictionary<PackageURL, double>? mapping = new();
            if (purl.Name is string purlName && (purlName.StartsWith('_') || npm_internal_modules.Contains(purlName)))
            {
                // url = 'https://github.com/nodejs/node/tree/master/lib' + package.name,

                mapping.Add(new PackageURL(purl.Type, purl.Namespace, purl.Name,
                    null, null, "node/tree/master/lib"), 1.0F);
                return mapping;
            }

            // if a version is provided, search that JSONElement, otherwise, just search the latest
            // version, which is more likely best maintained
            // TODO: If the latest version JSONElement doesnt have the repo infor, should we search all elements
            // on that chance that one of them might have it?
            JsonElement? versionJSON = string.IsNullOrEmpty(purl?.Version) ? GetLatestVersionElement(contentJSON) :
                GetVersionElement(contentJSON, new Version(purl.Version));

            if (versionJSON is JsonElement notNullVersionJSON)
            {
                try
                {
                    if (!notNullVersionJSON.TryGetProperty("repository", out JsonElement repository))
                    {
                        return mapping;
                    }
                    if (repository.ValueKind == JsonValueKind.Object)
                    {
                        string? repoType = OssUtilities.GetJSONPropertyStringIfExists(repository, "type")?.ToLower();
                        string? repoURL = OssUtilities.GetJSONPropertyStringIfExists(repository, "url");

                        // right now we deal with only github repos
                        if (repoType == "git" && repoURL is not null)
                        {
                            PackageURL gitPURL = GitHubProjectManager.ParseUri(new Uri(repoURL));
                            // we got a repository value the author specified in the metadata - so no
                            // further processing needed
                            mapping.Add(gitPURL, 1.0F);
                            return mapping;
                        }
                    }
                }
                catch (KeyNotFoundException) { /* continue onwards */ }
                catch (UriFormatException) {  /* the uri specified in the metadata invalid */ }
            }

            return mapping;
        }

        /// <summary>
        /// Internal Node.js modules that should be ignored when searching metadata.
        /// </summary>
        private static readonly List<string> npm_internal_modules = new()
        {
            "assert",
            "async_hooks",
            "buffer",
            "child_process",
            "cluster",
            "console",
            "constants",
            "crypto",
            "dgram",
            "dns",
            "domain",
            "events",
            "fs",
            "http",
            "http2",
            "https",
            "inspector",
            "module",
            "net",
            "os",
            "path",
            "perf_hooks",
            "process",
            "punycode",
            "querystring",
            "readline",
            "repl",
            "stream",
            "string_decoder",
            "timers",
            "tls",
            "trace_events",
            "tty",
            "url",
            "util",
            "v8",
            "vm",
            "zlib"
        };

        public enum NPMArtifactType
        {
            Unknown = 0,
            Tarball,
            PackageJson,
        }
    }
}