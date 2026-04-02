// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Microsoft.CST.OpenSource.Extensions;
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
    /// Project manager for NuGet V2 API endpoints, supports PowerShell Gallery and other V2 endpoints.
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
            : base(actions ?? NuGetPackageActions.CreateV2(POWER_SHELL_GALLERY_DEFAULT_INDEX), httpClientFactory ?? new DefaultHttpClientFactory(), directory, timeout)
        {
        }

        /// <summary>
        /// Gets the download URIs for artifacts associated with a package version.
        /// </summary>
        /// <param name="purl">The package URL specifying the package and version.</param>
        /// <param name="useCache">Whether to use cached data if available.</param>
        /// <returns>An async enumerable of artifact URIs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when package version is null.</exception>
        public override async IAsyncEnumerable<ArtifactUri<NuGetArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);

            // Get the repository URL from the package URL or use the default
            string repositoryUrl = purl.GetRepositoryUrlOrDefault(POWER_SHELL_GALLERY_DEFAULT_INDEX) ?? POWER_SHELL_GALLERY_DEFAULT_INDEX;

            yield return new ArtifactUri<NuGetArtifactType>(NuGetArtifactType.Nupkg, GetNupkgUrl(purl.Name, purl.Version, repositoryUrl));
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
            DateTime? uploadTime = (await GetPackageMetadataAsync(purl, useCache, includeRepositoryMetadata: false))?.UploadTime;
            return uploadTime;
        }

        /// <summary>
        /// Gets the URL for downloading a package's .nupkg file.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <param name="repositoryUrl">The repository URL to use for constructing the download URL.</param>
        /// <returns>The URL to download the .nupkg file.</returns>
        private static string GetNupkgUrl(string id, string version, string repositoryUrl)
        {
            string lowerId = id.ToLowerInvariant();
            string lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            return $"{repositoryUrl.TrimEnd('/')}/package/{lowerId}/{lowerVersion}";
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
