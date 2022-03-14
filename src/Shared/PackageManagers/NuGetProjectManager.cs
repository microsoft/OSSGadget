// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Helpers;
    using HtmlAgilityPack;
    using PackageUrl;
    using Model;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Utilities;
    using Version = SemanticVersioning.Version;


    public class NuGetProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public const string ENV_NUGET_ENDPOINT_API = "https://api.nuget.org";
        public const string ENV_NUGET_ENDPOINT = "https://www.nuget.org";
        public const string NUGET_DEFAULT_REGISTRATION_ENDPOINT = "https://api.nuget.org/v3/registration5-gz-semver2/";

        private string? RegistrationEndpoint { get; set; } = null;

        public NuGetProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
            GetRegistrationEndpointAsync().Wait();
        }

        public NuGetProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
            GetRegistrationEndpointAsync().Wait();
        }

        /// <summary>
        /// Dynamically identifies the registration endpoint.
        /// </summary>
        /// <returns>NuGet registration endpoint</returns>
        private async Task<string> GetRegistrationEndpointAsync()
        {
            if (RegistrationEndpoint != null)
            {
                return RegistrationEndpoint;
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_NUGET_ENDPOINT_API}/v3/index.json");
                JsonElement.ArrayEnumerator resources = doc.RootElement.GetProperty("resources").EnumerateArray();
                foreach (JsonElement resource in resources)
                {
                    try
                    {
                        string? _type = resource.GetProperty("@type").GetString();
                        if (_type != null && _type.Equals("RegistrationsBaseUrl/Versioned", StringComparison.InvariantCultureIgnoreCase))
                        {
                            string? _id = resource.GetProperty("@id").GetString();
                            if (!string.IsNullOrWhiteSpace(_id))
                            {
                                RegistrationEndpoint = _id;
                                return _id;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex, "Error parsing NuGet API endpoint: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error parsing NuGet API endpoint: {0}", ex.Message);
            }
            RegistrationEndpoint = NUGET_DEFAULT_REGISTRATION_ENDPOINT;
            return RegistrationEndpoint;
        }

        /// <summary>
        ///     Download one NuGet package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
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
                JsonDocument doc = await GetJsonCache(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json");
                List<string> versionList = new();
                foreach (JsonElement catalogPage in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (catalogPage.TryGetProperty("items", out JsonElement itemElement))
                    {
                        foreach (JsonElement item in itemElement.EnumerateArray())
                        {
                            JsonElement catalogEntry = item.GetProperty("catalogEntry");
                            string? version = catalogEntry.GetProperty("version").GetString();
                            if (version != null && version.Equals(packageVersion, StringComparison.InvariantCultureIgnoreCase))
                            {
                                string? archive = catalogEntry.GetProperty("packageContent").GetString();
                                HttpResponseMessage? result = await httpClient.GetAsync(archive);
                                result.EnsureSuccessStatusCode();
                                Logger.Debug("Downloading {0}...", purl?.ToString());

                                string? targetName = $"nuget-{packageName}@{packageVersion}";
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
                                    targetName += Path.GetExtension(archive) ?? "";
                                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                                    downloadedPaths.Add(targetName);
                                }
                                return downloadedPaths;
                            }
                        }
                    }
                    else
                    {
                        string? subDocUrl = catalogPage.GetProperty("@id").GetString();
                        if (subDocUrl != null)
                        {
                            JsonDocument subDoc = await GetJsonCache(httpClient, subDocUrl);
                            foreach (JsonElement subCatalogPage in subDoc.RootElement.GetProperty("items").EnumerateArray())
                            {
                                JsonElement catalogEntry = subCatalogPage.GetProperty("catalogEntry");
                                string? version = catalogEntry.GetProperty("version").GetString();
                                Logger.Debug("Identified {0} version {1}.", packageName, version);
                                if (version != null && version.Equals(packageVersion, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    string? archive = catalogEntry.GetProperty("packageContent").GetString();
                                    HttpResponseMessage? result = await httpClient.GetAsync(archive);
                                    result.EnsureSuccessStatusCode();
                                    Logger.Debug("Downloading {0}...", purl?.ToString());

                                    string targetName = $"nuget-{packageName}@{packageVersion}";
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
                                        targetName += Path.GetExtension(archive) ?? "";
                                        await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                                        downloadedPaths.Add(targetName);
                                    }
                                    return downloadedPaths;
                                }
                            }
                        }
                        else
                        {
                            Logger.Debug("Catalog identifier was null.");
                        }
                    }
                }
                Logger.Debug("Unable to find NuGet package.");
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading NuGet package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <summary>
        /// Check if the package exists in the respository.
        /// </summary>
        /// <param name="purl">The PackageURL to check.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <returns>True if the package is confirmed to exist in the repository. False otherwise.</returns>
        public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            HttpClient httpClient = CreateHttpClient();

            return await CheckJsonCacheForPackage(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json", useCache);
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());

            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                string packageName = purl.Name;

                JsonDocument doc = await GetJsonCache(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json", useCache);
                List<string> versionList = new();
                foreach (JsonElement catalogPage in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (catalogPage.TryGetProperty("items", out JsonElement itemElement))
                    {
                        foreach (JsonElement item in itemElement.EnumerateArray())
                        {
                            JsonElement catalogEntry = item.GetProperty("catalogEntry");
                            string? version = catalogEntry.GetProperty("version").GetString();
                            if (version != null)
                            {
                                Logger.Debug("Identified {0} version {1}.", packageName, version);
                                versionList.Add(version);
                            }
                            else
                            {
                                Logger.Debug("Identified {0} version NULL. This might indicate a parsing error.", packageName);
                            }
                        }
                    }
                    else
                    {
                        string? subDocUrl = catalogPage.GetProperty("@id").GetString();
                        if (subDocUrl != null)
                        {
                            JsonDocument subDoc = await GetJsonCache(httpClient, subDocUrl);
                            foreach (JsonElement subCatalogPage in subDoc.RootElement.GetProperty("items").EnumerateArray())
                            {
                                JsonElement catalogEntry = subCatalogPage.GetProperty("catalogEntry");
                                string? version = catalogEntry.GetProperty("version").GetString();
                                Logger.Debug("Identified {0} version {1}.", packageName, version);
                                if (version != null)
                                {
                                    Logger.Debug("Identified {0} version {1}.", packageName, version);
                                    versionList.Add(version);
                                }
                                else
                                {
                                    Logger.Debug("Identified {0} version NULL. This might indicate a parsing error.", packageName);
                                }
                            }
                        }
                        else
                        {
                            Logger.Debug("Catalog identifier was null.");
                        }
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

        public override async Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
        {
            try
            {
                string? packageName = purl?.Name;
                if (packageName == null)
                {
                    return null;
                }
                HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json", useCache);
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching NuGet metadata: {ex.Message}");
                return null;
            }
        }
        
        public async Task<JsonElement?> GetVersionUriMetadata(string versionUri, bool useCache = true)
        {
            try
            {
                HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, versionUri, useCache);
                if (string.IsNullOrEmpty(content)) { return null; }

                // convert NuGet package data to normalized form
                JsonDocument contentJSON = JsonDocument.Parse(content);
                return contentJSON.RootElement;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching - {versionUri}: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_NUGET_HOMEPAGE}/{purl?.Name}");
        }
        
        /// <inheritdoc />
        public override async Task<PackageMetadata> GetPackageMetadata(PackageURL purl, bool useCache = true)
        {
            PackageMetadata metadata = new();
            string? content = await GetMetadata(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return metadata; }

            // convert NuGet package data to normalized form
            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;
            JsonElement latestCatalogEntry = GetLatestCatalogEntry(root);

            metadata.Name = latestCatalogEntry.GetProperty("id").GetString();
            metadata.Description = latestCatalogEntry.GetProperty("description").GetString();
            
            // Title is different than name or description for NuGet packages, not always used.
            // metadata.Title = GetLatestCatalogEntry(root).GetProperty("title").GetString();

            metadata.PackageManagerUri = ENV_NUGET_ENDPOINT;
            metadata.Platform = "NUGET";
            metadata.Language = "C#";
            // metadata.SpokenLanguage = GetLatestCatalogEntry(root).GetProperty("language").GetString();
            metadata.PackageUri = $"{metadata.PackageManagerUri}/packages/{metadata.Name?.ToLower()}";
            metadata.ApiPackageUri = $"{RegistrationEndpoint}{metadata.Name?.ToLower()}/index.json";

            List<Version> versions = GetVersions(contentJSON);
            Version? latestVersion = GetLatestVersion(versions);

            if (purl.Version != null)
            {
                // find the version object from the collection
                metadata.PackageVersion = purl.Version;
            }
            else
            {
                metadata.PackageVersion = latestVersion is null ? purl.Version : latestVersion.ToString();
            }

            // if we found any version at all, get the info
            if (metadata.PackageVersion != null)
            {
                Version versionToGet = new(metadata.PackageVersion);
                JsonElement? versionElement = GetVersionElement(contentJSON, versionToGet);
                if (versionElement != null)
                {
                    // redo the generic values to version specific values
                    metadata.VersionUri = $"{metadata.PackageManagerUri}/packages/{metadata.Name?.ToLower()}/{versionToGet}";
                    metadata.ApiVersionUri = OssUtilities.GetJSONPropertyStringIfExists(versionElement, "@id");

                    JsonElement versionContent = await this.GetVersionUriMetadata(metadata.ApiVersionUri!, useCache) ?? throw new InvalidOperationException();
                    
                    // Get the artifact contents url
                    JsonElement? packageContent = OssUtilities.GetJSONPropertyIfExists(versionElement, "packageContent");
                    if (packageContent != null)
                    {
                        metadata.VersionDownloadUri = packageContent.ToString();
                    }
                    
                    // size and hash from versionContent
                    metadata.Size = OssUtilities.GetJSONPropertyIfExists(versionContent, "packageSize")?.GetInt64();
                    metadata.Signature ??= new List<Digest>();
                    metadata.Signature.Add(new Digest
                    {
                        Algorithm = OssUtilities.GetJSONPropertyStringIfExists(versionContent, "packageHashAlgorithm"),
                        Signature = OssUtilities.GetJSONPropertyStringIfExists(versionContent, "packageHash"),
                    });
                    
                    // homepage
                    metadata.Homepage = OssUtilities.GetJSONPropertyStringIfExists(versionContent, "projectUrl");

                    // author(s)
                    JsonElement? authorElement = OssUtilities.GetJSONPropertyIfExists(versionElement, "authors");
                    User author = new();
                    if (authorElement is not null)
                    {
                        author.Name = authorElement?.GetString();
                        // TODO: User email and url
                        // author.Email = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "email");
                        // author.Url = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "url");

                        metadata.Authors ??= new List<User>();
                        metadata.Authors.Add(author);
                    }

                    // TODO: maintainers

                    // repository
                    PackageURL nuspecPurl = purl;

                    // If no version specified, get it for the latest version
                    if (nuspecPurl.Version.IsBlank())
                    {
                        nuspecPurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, metadata.PackageVersion,
                            purl.Qualifiers, purl.Subpath);
                    }
                    NuspecReader? nuspecReader = GetNuspec(nuspecPurl);
                    RepositoryMetadata? repositoryMetadata = nuspecReader?.GetRepositoryMetadata();
                    if (repositoryMetadata != null)
                    {
                        if (GitHubProjectManager.IsGitHubRepoUrl(repositoryMetadata.Url, out PackageURL? githubPurl))
                        {
                            Repository repository = new()
                            {
                                Type = "github"
                            };
                        
                            await repository.ExtractRepositoryMetadata(githubPurl!);

                            metadata.Repository ??= new List<Repository>();
                            metadata.Repository.Add(repository);
                        }
                    }
                    
                    // dependencies
                    IList<PackageDependencyGroup>? dependencyGroups = nuspecReader?.GetDependencyGroups().ToList();
                    if (dependencyGroups is not null && dependencyGroups.Any())
                    {
                        metadata.Dependencies ??= new List<Dependency>();

                        foreach (PackageDependencyGroup dependencyGroup in dependencyGroups)
                        {
                            dependencyGroup.Packages.ToList().ForEach((dependency) => metadata.Dependencies.Add(new Dependency() { Package = dependency.ToString(), Framework = dependencyGroup.TargetFramework.ToString()}));
                        }
                    }

                    // keywords
                    metadata.Keywords = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "tags"));

                    // licenses
                    metadata.Licenses ??= new List<License>();
                    metadata.Licenses.Add(new License()
                    {
                        Name = OssUtilities.GetJSONPropertyStringIfExists(versionElement, "licenseExpression"),
                        Url = OssUtilities.GetJSONPropertyStringIfExists(versionElement, "licenseUrl")
                    });
                    
                    // publishing info
                    metadata.UploadTime = OssUtilities.GetJSONPropertyStringIfExists(versionElement, "published");
                }
            }

            if (latestVersion is not null)
            {
                metadata.LatestPackageVersion = latestVersion.ToString();
            }

            return metadata;
        }

        public JsonElement GetLatestCatalogEntry(JsonElement root)
        {
            return root.GetProperty("items").EnumerateArray().Last() // Last CatalogPage
                .GetProperty("items").EnumerateArray().Last() // Last Package
                .GetProperty("catalogEntry"); // Get the entry for the most recent package version
        }
        
        public override JsonElement? GetVersionElement(JsonDocument? contentJSON, Version desiredVersion)
        {
            if (contentJSON is null) { return null; }
            JsonElement root = contentJSON.RootElement;

            try
            {
                JsonElement catalogPages = root.GetProperty("items");
                foreach (JsonElement page in catalogPages.EnumerateArray())
                {
                    JsonElement entries = page.GetProperty("items");
                    foreach (JsonElement entry in entries.EnumerateArray())
                    {
                        string version = entry.GetProperty("catalogEntry").GetProperty("version").GetString() 
                                         ?? throw new InvalidOperationException();
                        if (string.Equals(version, desiredVersion.ToString(), StringComparison.InvariantCultureIgnoreCase))
                        {
                            return entry.GetProperty("catalogEntry");
                        }
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
                JsonElement catalogPages = root.GetProperty("items");
                foreach (JsonElement page in catalogPages.EnumerateArray())
                {
                    JsonElement entries = page.GetProperty("items");
                    foreach (JsonElement entry in entries.EnumerateArray())
                    {
                        string version = entry.GetProperty("catalogEntry").GetProperty("version").GetString() 
                                         ?? throw new InvalidOperationException();
                        allVersions.Add(new Version(version));
                    }
                }
            }
            catch (KeyNotFoundException) { return allVersions; }
            catch (InvalidOperationException) { return allVersions; }

            return allVersions;
        }
        
        protected override async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
        {
            Dictionary<PackageURL, double> mapping = new();
            try
            {
                string? packageName = purl.Name;

                // nuget doesnt provide repository information in the json metadata; we have to extract it
                // from the html home page
                HtmlWeb web = new();
                HtmlDocument doc = web.Load($"{ENV_NUGET_HOMEPAGE}/{packageName}");

                List<string> paths = new()
                {
                    "//a[@title=\"View the source code for this package\"]/@href",
                    "//a[@title=\"Visit the project site to learn more about this package\"]/@href"
                };
                await Task.Run(() =>
                {
                    foreach (string path in paths)
                    {
                        string? repoCandidate = doc.DocumentNode.SelectSingleNode(path)?.GetAttributeValue("href", string.Empty);
                        if (!string.IsNullOrEmpty(repoCandidate))
                        {
                            PackageURL? candidate = ExtractGitHubPackageURLs(repoCandidate).FirstOrDefault();
                            if (candidate != null)
                            {
                                mapping[candidate] = 1.0F;
                            }
                        }
                    }
                });
                return mapping;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching/parsing NuGet homepage: {ex.Message}");
            }

            // if nothing worked, return empty
            return mapping;
        }


        private NuspecReader? GetNuspec(PackageURL purl)
        {
            string uri = $"{ENV_NUGET_ENDPOINT_API}/v3-flatcontainer/{purl.Name.ToLower()}/{purl.Version}/{purl.Name.ToLower()}.nuspec";
            try
            {
                HttpClient httpClient = this.CreateHttpClient();
                HttpResponseMessage response = httpClient.GetAsync(uri).GetAwaiter().GetResult();
                using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                {
                    return new(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        private static string ENV_NUGET_HOMEPAGE = "https://www.nuget.org/packages";
    }
}
