// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Microsoft.CST.OpenSource.Contracts;
    using Microsoft.CST.OpenSource.Helpers;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.Model.Metadata;
    using Microsoft.CST.OpenSource.PackageActions;
    using NuGet.Versioning;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Project manager for NuGet V2 API endpoints, primarily used for PowerShell Gallery integration.
    /// </summary>
    public class NuGetV2ProjectManager : BaseNuGetProjectManager
    {
        /// <summary>
        /// The default index URL for PowerShell Gallery V2 API.
        /// </summary>
        public const string POWER_SHELL_GALLERY_DEFAULT_INDEX = "https://www.powershellgallery.com/api/v2";
        public const string POWER_SHELL_GALLERY_DEFAULT_CONTENT_ENDPOINT = "https://www.powershellgallery.com/packages/";

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetV2ProjectManager"/> class.
        /// </summary>
        /// <param name="directory">The working directory for package operations.</param>
        /// <param name="actions">Optional custom package actions implementation. If null, creates default V2 actions.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory. If null, uses default factory.</param>
        /// <param name="timeout">Optional timeout for operations. If null, uses default timeout.</param>
        public NuGetV2ProjectManager(
            string directory,
            IManagerPackageActions<NuGetPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null,
            TimeSpan? timeout = null)
            : base(actions ?? NuGetPackageActions.CreateV2(), httpClientFactory ?? new DefaultHttpClientFactory(), directory, timeout)
        {
        }

        /// <summary>
        /// Gets the download URIs for artifacts associated with a package version.
        /// </summary>
        /// <param name="purl">The package URL specifying the package and version.</param>
        /// <param name="useCache">Whether to use cached data if available.</param>
        /// <returns>An async enumerable of artifact URIs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when package version is null.</exception>
        /// <exception cref="NotImplementedException">Thrown when repository URL is not PowerShell Gallery.</exception>
        public override async IAsyncEnumerable<ArtifactUri<NuGetArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            if (purl.Qualifiers?.TryGetValue("repository_url", out string? repositoryUrlQualifier) == true && repositoryUrlQualifier.Trim('/') != POWER_SHELL_GALLERY_DEFAULT_INDEX)
            {
                // Throw an exception until we implement proper support for service indices other than nuget.org  
                throw new NotImplementedException(
                    $"NuGet package URLs having a repository URL other than '{POWER_SHELL_GALLERY_DEFAULT_INDEX}' are not currently supported.");
            }

            yield return new ArtifactUri<NuGetArtifactType>(NuGetArtifactType.Nupkg, NuGetV2ProjectManager.GetNupkgUrl(purl.Name, purl.Version));
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

        /// <summary>
        /// Gets the URL for downloading a package's .nupkg file.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The URL to download the .nupkg file.</returns>
        private static string GetNupkgUrl(string id, string version)
        {
            string lowerId = id.ToLowerInvariant();
            string lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            return $"{POWER_SHELL_GALLERY_DEFAULT_INDEX.TrimEnd('/')}/package/{lowerId}/{lowerVersion}";
        }

        /// <summary>
        /// Gets packages owned by a specific user or organization.
        /// </summary>
        /// <param name="owner">The owner's username.</param>
        /// <param name="useCache">Whether to use cached data if available.</param>
        /// <returns>An async enumerable of package URLs.</returns>
        /// <exception cref="NotImplementedException">This operation is not currently implemented.</exception>
        public override IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true) => throw new NotImplementedException();

        /// <inheritdoc />
        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true, bool includeRepositoryMetadata = true)
        {
            string? latestVersion = await Actions.GetLatestVersionAsync(purl, includePrerelease: includePrerelease, useCache: useCache);

            // Construct a new PackageURL that's guaranteed to have a version, the latest version is used if no version was provided.
            PackageURL purlWithVersion = !string.IsNullOrWhiteSpace(purl.Version) ?
                purl : new PackageURL(purl.Type, purl.Namespace, purl.Name, latestVersion, purl.Qualifiers, purl.Subpath);

            NuGetPackageVersionMetadata? packageVersionMetadata =
                await Actions.GetMetadataAsync(purlWithVersion, useCache: useCache);

            if (packageVersionMetadata is null)
            {
                return null;
            }

            PackageMetadata metadata = new()
            {
                Name = packageVersionMetadata.Name,
                Description = packageVersionMetadata.Description,
                PackageVersion = purlWithVersion.Version,
                LatestPackageVersion = latestVersion,
                UploadTime = packageVersionMetadata.Published?.DateTime
            };

            return metadata;
        }

    }
}
