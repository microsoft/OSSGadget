// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Contracts;
    using PackageUrl;
    using Microsoft.CST.OpenSource.Model.Metadata;
    using Microsoft.CST.OpenSource.PackageActions;
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

        
    
        /// Creates an instance of a BaseNuGetProjectManager based on the provided PackageURL.
        /// If the repository URL matches the PowerShell Gallery URL, a NuGetV2ProjectManager is created.
        /// Otherwise, a default NuGetProjectManager (V3) is created.
        internal static BaseNuGetProjectManager Create(string destinationDirectory, IHttpClientFactory httpClientFactory, TimeSpan? timeout, PackageURL? packageUrl)
        {
            // Check if the repository_url exists and matches the PowerShell Gallery URL
            if (packageUrl?.TryGetRepositoryUrl(out string? repositoryUrlQualifier) == true &&
                repositoryUrlQualifier!.TrimEnd('/') == NuGetV2ProjectManager.POWER_SHELL_GALLERY_DEFAULT_INDEX)
            {
                return new NuGetV2ProjectManager(destinationDirectory, NuGetPackageActions.CreateV2(), httpClientFactory, timeout);
            }

            // Default case: Use NuGetProjectManager (V3)
            return new NuGetProjectManager(destinationDirectory, NuGetPackageActions.CreateV3(), httpClientFactory, timeout);
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true)
        {
            throw new NotImplementedException("Haven't found a way to implement this yet!");
            yield break;
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
