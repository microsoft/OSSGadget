// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using PackageUrl;
    using Model.Metadata;
    using PackageActions;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;

    public abstract class BaseNuGetProjectManager : TypedManager<NuGetPackageVersionMetadata, BaseNuGetProjectManager.NuGetArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "nuget";

        public override string ManagerType => Type;

        protected BaseNuGetProjectManager(IManagerPackageActions<NuGetPackageVersionMetadata> actions, IHttpClientFactory httpClientFactory, string directory, TimeSpan? timeout = null) : base(actions, httpClientFactory, directory, timeout)
        {
        }

        public enum NuGetArtifactType
        {
            Unknown = 0,
            Nupkg,
            Nuspec,
        }

        internal static BaseNuGetProjectManager Create(string destinationDirectory, IHttpClientFactory httpClientFactory, TimeSpan? timeout, PackageURL? packageUrl)
        {
            if (packageUrl is null || packageUrl.Qualifiers?.TryGetValue("repository_url", out string? repositoryUrlQualifier) != true ||
                repositoryUrlQualifier?.TrimEnd('/') == NuGetProjectManager.NUGET_DEFAULT_INDEX)
            {
                return new NuGetProjectManager(destinationDirectory, NuGetPackageActions.CreateV3(), httpClientFactory, timeout);
            }
            else if (repositoryUrlQualifier?.TrimEnd('/') == NuGetV2ProjectManager.POWER_SHELL_GALLERY_DEFAULT_INDEX)
            {
                return new NuGetV2ProjectManager(destinationDirectory, NuGetPackageActions.CreateV2(), httpClientFactory, timeout);
            }
            else
            {
                throw new NotImplementedException($"NuGet package URLs having a repository URL other than '{NuGetV2ProjectManager.POWER_SHELL_GALLERY_DEFAULT_INDEX}' or '{NuGetProjectManager.NUGET_DEFAULT_INDEX}' are not currently supported.");
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true)
        {
            throw new NotImplementedException("Haven't found a way to implement this yet!");
            yield break;
        }

        /// <summary>  
        /// Retrieves metadata for a specific NuGet package version.  
        /// If no version is provided in the <paramref name="purl"/>, the latest version is used.  
        /// </summary>  
        /// <param name="purl">The <see cref="PackageURL"/> representing the package to retrieve metadata for.</param>  
        /// <param name="includePrerelease">Indicates whether to include pre-release versions when determining the latest version. Defaults to <c>false</c>.</param>  
        /// <param name="useCache">Indicates whether to use cached data for the request. Defaults to <c>true</c>.</param>  
        /// <param name="includeRepositoryMetadata">Indicates whether to include repository metadata in the result. Defaults to <c>true</c>.</param>  
        /// <returns>A <see cref="NuGetPackageVersionMetadata"/> object containing metadata for the specified package version, or <c>null</c> if no metadata is found.</returns>  
        /// <remarks>  
        /// This method first determines the latest version of the package if no version is specified in the <paramref name="purl"/>.  
        /// It then retrieves metadata for the specified or latest version of the package.  
        /// </remarks>  
        protected async Task<NuGetPackageVersionMetadata?> GetNuGetPackageVersionMetadata(PackageURL purl, bool includePrerelease = false, bool useCache = true, bool includeRepositoryMetadata = true)
        {
            string? latestVersion = await Actions.GetLatestVersionAsync(purl, includePrerelease: includePrerelease, useCache: useCache);

            // Construct a new PackageURL that's guaranteed to have a version, the latest version is used if no version was provided.  
            PackageURL purlWithVersion = !string.IsNullOrWhiteSpace(purl.Version) ?
                purl : new PackageURL(purl.Type, purl.Namespace, purl.Name, latestVersion, purl.Qualifiers, purl.Subpath);

            NuGetPackageVersionMetadata? packageVersionMetadata =
                await Actions.GetMetadataAsync(purlWithVersion, useCache: useCache);

            return packageVersionMetadata;
        }

        /// <summary>
        /// Gets if the package is a part of a reserved prefix.
        /// </summary>
        /// <param name="purl">The package url to check.</param>
        /// <param name="useCache">If the cache should be used.</param>
        /// <returns>True if the package is verified to be in a reserved prefix, false if not.</returns>
        public async Task<bool> GetHasReservedNamespaceAsync(PackageURL purl, bool useCache = true)
        {
            return await Actions.GetHasReservedNamespaceAsync(purl, useCache: useCache);
        }

    }
}
