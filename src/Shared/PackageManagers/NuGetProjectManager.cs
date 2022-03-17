// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

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
    using NuGet.Versioning;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Repository = Model.Repository;

    public class NuGetProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public const string ENV_NUGET_ENDPOINT_API = "https://api.nuget.org";
        public const string ENV_NUGET_ENDPOINT = "https://www.nuget.org";
        public const string NUGET_DEFAULT_REGISTRATION_ENDPOINT = "https://api.nuget.org/v3/registration5-gz-semver2/";
        private const string NUGET_DEFAULT_CONTENT_ENDPOINT = "https://api.nuget.org/v3-flatcontainer/";

        private string? RegistrationEndpoint { get; set; } = null;

        private SourceCacheContext _sourceCacheContext = new();
        private SourceRepository _sourceRepository = NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

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
        /// Download one NuGet package and extract it to the target directory.
        /// </summary>
        /// <remarks>The target directory is defined when creating the <see cref="NuGetProjectManager"/> in the subdirectory named `nuget-{packagename}@{packageversion}`</remarks>
        /// <param name="purl">The <see cref="PackageURL"/> of the package to download.</param>
        /// <param name="doExtract">If the contents of the .nupkg should be extracted into a directory.</param>
        /// <param name="cached">If the downloaded contents should be retrieved from the cache if they exist there.</param>
        /// <returns>An IEnumerable list of the path(s) the contents were downloaded to.</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl.ToString());

            string? packageName = purl.Name;
            string? packageVersion = purl.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                CancellationToken cancellationToken = CancellationToken.None;

                FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

                string targetName = $"nuget-{packageName}@{packageVersion}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                if (doExtract && Directory.Exists(extractionPath) && cached)
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }

                using MemoryStream packageStream = new MemoryStream();

                bool downloaded = await resource.CopyNupkgToStreamAsync(
                    purl.Name,
                    NuGetVersion.Parse(purl.Version),
                    packageStream,
                    _sourceCacheContext,
                    NullLogger.Instance, 
                    cancellationToken);

                // If the .nupkg wasn't downloaded.
                if (!downloaded)
                {
                    return downloadedPaths;
                }

                if (doExtract)
                {
                    downloadedPaths.Add(await ExtractArchive(targetName, packageStream.ToArray(), cached));
                }
                else
                {
                    targetName += ".nupkg";
                    string filePath = Path.Combine(TopLevelExtractionDirectory, targetName);
                    await File.WriteAllBytesAsync(filePath, packageStream.ToArray());
                    downloadedPaths.Add(filePath);
                }

                return downloadedPaths;
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

            CancellationToken cancellationToken = CancellationToken.None;

            FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

            bool exists = await resource.DoesPackageExistAsync(
                purl.Name,
                NuGetVersion.Parse(purl.Version),
                _sourceCacheContext,
                NullLogger.Instance, 
                cancellationToken);

            return exists;
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl.ToString());

            if (purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                CancellationToken cancellationToken = CancellationToken.None;

                FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

                IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
                    purl.Name,
                    _sourceCacheContext,
                    NullLogger.Instance, 
                    cancellationToken);

                // Sort versions, highest first, lowest last.
                return SortVersions(versions.Select(v => v.ToString()));
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
                string? packageName = purl.Name;
                if (packageName == null)
                {
                    return null;
                }

                CancellationToken cancellationToken = CancellationToken.None;

                PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();

                string latestVersion = (await EnumerateVersions(purl, useCache)).Last();

                PackageIdentity packageIdentity = !string.IsNullOrEmpty(purl.Version) ? 
                    new PackageIdentity(purl.Name, NuGetVersion.Parse(purl.Version)) : 
                    new PackageIdentity(purl.Name, NuGetVersion.Parse(latestVersion));
            
                PackageSearchMetadataRegistration? packageVersion = await resource.GetMetadataAsync(
                    packageIdentity,
                    _sourceCacheContext,
                    NullLogger.Instance, 
                    cancellationToken) as PackageSearchMetadataRegistration;

                return packageVersion?.ToJson();
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

            PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();

            string latestVersion = (await EnumerateVersions(purl, useCache)).Last();

            PackageIdentity packageIdentity = !string.IsNullOrEmpty(purl.Version) ? 
                new PackageIdentity(purl.Name, NuGetVersion.Parse(purl.Version)) : 
                new PackageIdentity(purl.Name, NuGetVersion.Parse(latestVersion));
            
            PackageSearchMetadataRegistration? packageVersion = await resource.GetMetadataAsync(
                packageIdentity,
                _sourceCacheContext,
                NullLogger.Instance, 
                cancellationToken) as PackageSearchMetadataRegistration;

            if (packageVersion is null)
            {
                throw new NullReferenceException();
            }

            PackageMetadata metadata = new();

            metadata.Name = packageVersion.PackageId;
            metadata.Description = packageVersion.Description;

            // Title is different than name or description for NuGet packages, not always used.
            // metadata.Title = packageVersion.Title;

            metadata.PackageManagerUri = ENV_NUGET_ENDPOINT_API;
            metadata.Platform = "NUGET";
            metadata.Language = "C#";
            metadata.PackageUri = $"{ENV_NUGET_HOMEPAGE}/{metadata.Name?.ToLowerInvariant()}";
            metadata.ApiPackageUri = $"{RegistrationEndpoint}{metadata.Name?.ToLowerInvariant()}/index.json";

            metadata.PackageVersion = purl.Version ?? latestVersion;
            metadata.LatestPackageVersion = latestVersion;

            // if we found any version at all, get the info
            if (metadata.PackageVersion != null)
            {
                string? nameLowercase = metadata.Name?.ToLowerInvariant();

                // Set the version specific URI values.
                metadata.VersionUri = $"{metadata.PackageManagerUri}/packages/{nameLowercase}/{metadata.PackageVersion}";
                metadata.ApiVersionUri = packageVersion.CatalogUri.ToString();
                
                // Construct the artifact contents url.
                metadata.VersionDownloadUri = GetNupkgUrl(metadata.Name!, metadata.PackageVersion);

                // TODO: size and hash

                // homepage
                metadata.Homepage = packageVersion.ProjectUrl?.ToString();

                // author(s)
                string? author = packageVersion.Authors;
                if (author is not null)
                {
                    metadata.Authors ??= new List<User>();
                    metadata.Authors.Add(new User(){Name = author});
                }

                // TODO: maintainers

                // .nuspec parsing

                // repository
                NuspecReader? nuspecReader = GetNuspec(metadata.Name!, metadata.PackageVersion);
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
                IList<PackageDependencyGroup> dependencyGroups = packageVersion.DependencySets.ToList();
                if (dependencyGroups.Any())
                {
                    metadata.Dependencies ??= new List<Dependency>();

                    foreach (PackageDependencyGroup dependencyGroup in dependencyGroups)
                    {
                        dependencyGroup.Packages.ToList().ForEach((dependency) => metadata.Dependencies.Add(new Dependency() { Package = dependency.ToString(), Framework = dependencyGroup.TargetFramework?.ToString()}));
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

            return metadata;
        }

        /// <summary>
        /// Helper method to get the URL to download a NuGet package's .nupkg.
        /// </summary>
        /// <param name="id">The id/name of the package to get the .nupkg for.</param>
        /// <param name="version">The version of the package to get the .nupkg for.</param>
        /// <returns>The URL for the nupkg file.</returns>
        private static string GetNupkgUrl(string id, string version)
        {
            string lowerId = id.ToLowerInvariant();
            string lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            string url = $"{NUGET_DEFAULT_CONTENT_ENDPOINT.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg";
            return url;
        }
        
        private NuspecReader? GetNuspec(string id, string version)
        {
            string lowerId = id.ToLowerInvariant();
            string lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            string uri = $"{NUGET_DEFAULT_CONTENT_ENDPOINT.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.nuspec";
            try
            {
                HttpClient httpClient = this.CreateHttpClient();
                HttpResponseMessage response = httpClient.GetAsync(uri).GetAwaiter().GetResult();
                using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                {
                    return new NuspecReader(stream);
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
