﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Helpers;
    using HtmlAgilityPack;
    using PackageUrl;
    using Model;
    using NuGet.Common;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Repository = Model.Repository;
    using Version = SemanticVersioning.Version;

    public class NuGetProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public const string ENV_NUGET_ENDPOINT_API = "https://api.nuget.org";
        public const string ENV_NUGET_ENDPOINT = "https://www.nuget.org";
        public const string NUGET_DEFAULT_REGISTRATION_ENDPOINT = "https://api.nuget.org/v3/registration5-gz-semver2/";
        private const string NUGET_DEFAULT_CONTENT_ENDPOINT = "https://api.nuget.org/v3-flatcontainer/";

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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());

            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                CancellationToken cancellationToken = CancellationToken.None;

                SourceCacheContext cache = new SourceCacheContext();
                SourceRepository repository = NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
                PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

                IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
                    purl.Name,
                    includePrerelease: true,
                    includeUnlisted: false,
                    cache,
                    NullLogger.Instance,
                    cancellationToken);

                IEnumerable<IPackageSearchMetadata> packagesList = packages.ToList();
                List<string> versionList = packagesList.Select((p) => p.Identity.Version.ToString()).ToList();

                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
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

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_NUGET_HOMEPAGE}/{purl?.Name}");
        }
        
        /// <inheritdoc />
        public override async Task<PackageMetadata> GetPackageMetadata(PackageURL purl, bool useCache = true)
        {
            CancellationToken cancellationToken = CancellationToken.None;

            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

            IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
                purl.Name,
                includePrerelease: true,
                includeUnlisted: false,
                cache,
                NullLogger.Instance,
                cancellationToken);

            IEnumerable<IPackageSearchMetadata> packagesList = packages.ToList();
            PackageSearchMetadataRegistration? packageVersion = packagesList.Single(p => p.Identity.Version.OriginalVersion == purl.Version) as PackageSearchMetadataRegistration;

            if (packageVersion is null)
            {
                throw new NullReferenceException();
            }
            PackageMetadata metadata = new();

            metadata.Name = packageVersion.PackageId;
            metadata.Description = packageVersion.Description;
            
            // Title is different than name or description for NuGet packages, not always used.
            // metadata.Title = GetLatestCatalogEntry(root).GetProperty("title").GetString();

            metadata.PackageManagerUri = ENV_NUGET_ENDPOINT_API;
            metadata.Platform = "NUGET";
            metadata.Language = "C#";
            // metadata.SpokenLanguage = GetLatestCatalogEntry(root).GetProperty("language").GetString();
            metadata.PackageUri = $"{metadata.PackageManagerUri}/packages/{metadata.Name?.ToLowerInvariant()}";
            metadata.ApiPackageUri = $"{RegistrationEndpoint}{metadata.Name?.ToLowerInvariant()}/index.json";

            IEnumerable<Version> versions = packagesList.Select((p) => new Version(p.Identity.Version.ToString()));
            Version? latestVersion = GetLatestVersion(versions.ToList());

            if (purl.Version != null)
            {
                // find the version object from the collection
                metadata.PackageVersion = purl.Version;
            }
            else
            {
                metadata.PackageVersion = latestVersion.ToString();
            }

            // if we found any version at all, get the info
            if (metadata.PackageVersion != null)
            {
                Version versionToGet = new(metadata.PackageVersion);
                string? nameLowercase = metadata.Name?.ToLowerInvariant();
                // redo the generic values to version specific values
                metadata.VersionUri = $"{metadata.PackageManagerUri}/packages/{nameLowercase}/{versionToGet}";
                metadata.ApiVersionUri = packageVersion.CatalogUri.ToString();
                
                // Get the artifact contents url
                metadata.VersionDownloadUri = $"{NUGET_DEFAULT_CONTENT_ENDPOINT}{nameLowercase}/{versionToGet}/{nameLowercase}.{versionToGet}.nupkg";


                // TODO: size and hash

                // homepage
                metadata.Homepage = packageVersion.ProjectUrl.ToString();

                // author(s)
                string? author = packageVersion.Authors;
                if (author is not null)
                {
                    metadata.Authors ??= new List<User>();
                    metadata.Authors.Add(new User(){Name = author});
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
                        Repository ghRepository = new()
                        {
                            Type = "github"
                        };
                    
                        await ghRepository.ExtractRepositoryMetadata(githubPurl!);

                        metadata.Repository ??= new List<Repository>();
                        metadata.Repository.Add(ghRepository);
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
                metadata.Keywords = new List<string>(packageVersion.Tags.Split(", "));

                // licenses
                if (packageVersion.LicenseMetadata is not null)
                {
                    metadata.Licenses ??= new List<License>();
                    metadata.Licenses.Add(new License()
                    {
                        Name = packageVersion.LicenseMetadata.License,
                        Url = packageVersion.LicenseMetadata.LicenseUrl.ToString()
                    });
                }

                // publishing info
                metadata.UploadTime = packageVersion.Published?.ToString("MM/dd/yy HH:mm:ss zz");
            }

            if (latestVersion is not null)
            {
                metadata.LatestPackageVersion = latestVersion.ToString();
            }

            return metadata;
        }

        private NuspecReader? GetNuspec(PackageURL purl)
        {
            string uri = $"{NUGET_DEFAULT_CONTENT_ENDPOINT}{purl.Name.ToLower()}/{purl.Version}/{purl.Name.ToLower()}.nuspec";
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
