// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Microsoft.CST.OpenSource.Helpers;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class CondaProjectManager : TypedManager<IManagerPackageVersionMetadata, CondaProjectManager.CondaArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "conda";
        public const string DEFAULT_CONDA_ENDPOINT = "https://repo.anaconda.com/pkgs";

        public override string ManagerType => Type;
        
        private const string TarBz2FileType = "tar.bz2";
        private const string CondaFileType = "conda";
        private const string BuildQualifier = "build";
        private const string SubdirQualifier = "subdir";
        private const string TypeQualifier = "type";
        private const string DefaultChannel = "main";

        public CondaProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory)
        {
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ArtifactUri<CondaArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl), purl);
            if (!IsValidPackageUrlForConda(purl))
            {
                throw new ArgumentException("Invalid package URL for Conda.");
            }

            string? type = purl?.Qualifiers?.GetValueOrDefault(TypeQualifier);
            if (type is null)
            {
                string condaFileUrl = GetPackageDownloadUrl(purl!, CondaFileType);
                yield return new ArtifactUri<CondaArtifactType>(CondaArtifactType.Conda, condaFileUrl);

                string tarBz2Url = GetPackageDownloadUrl(purl!, TarBz2FileType);
                yield return new ArtifactUri<CondaArtifactType>(CondaArtifactType.TarBz2, tarBz2Url);
            }
            else if (type is CondaFileType)
            {
                string downloadUrl = GetPackageDownloadUrl(purl!);
                yield return new ArtifactUri<CondaArtifactType>(CondaArtifactType.Conda, downloadUrl);
                
            }
            else if (type is TarBz2FileType)
            {
                string downloadUrl = GetPackageDownloadUrl(purl!);
                yield return new ArtifactUri<CondaArtifactType>(CondaArtifactType.TarBz2, downloadUrl);
            }
            else
            {
                throw new ArgumentException($"Package 'type' for Conda must be '{CondaFileType}' or '{TarBz2FileType}'.");
            }

        }

        /// <inheritdoc />
        public override IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true) => throw new NotImplementedException();

        /// <inheritdoc />
        public override async Task<bool> PackageVersionExistsAsync(PackageURL purl, bool useCache = true)
        {
            if (!IsValidPackageUrlForConda(purl))
            {
                return false;
            }
            string downloadUrl = GetPackageDownloadUrl(purl);
            HttpClient httpClient = CreateHttpClient();
            return await CheckHttpCacheForPackage(httpClient, downloadUrl, useCache);
        }

        private bool IsValidPackageUrlForConda(PackageURL purl)
        {
            string? name = purl?.Name;
            string? version = purl?.Version;
            string? build = purl?.Qualifiers?.GetValueOrDefault(BuildQualifier);
            string? subDir = purl?.Qualifiers?.GetValueOrDefault(SubdirQualifier);
            string? type = purl?.Qualifiers?.GetValueOrDefault(TypeQualifier);

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version) || string.IsNullOrEmpty(build) || string.IsNullOrEmpty(subDir))
            {
                return false;
            }

            if (type is not null and not (CondaFileType or TarBz2FileType))
            {
                return false;
            }

            return true;
        }

        private static string GetPackageDownloadUrl(PackageURL purl, string defaultType = TarBz2FileType)
        {
            // Pre-condition: the provided purl has been validated by 'IsValidPackageUrlForConda'.
            string name = purl.Name;
            string version = purl.Version;
            string build = purl.Qualifiers[BuildQualifier];
            string subDir = purl.Qualifiers[SubdirQualifier];

            string channel = purl?.Qualifiers?.GetValueOrDefault("channel") ?? DefaultChannel;
            string type = purl?.Qualifiers?.GetValueOrDefault(TypeQualifier) ?? defaultType;
            string feedUrl = (purl.Qualifiers?.GetValueOrDefault("repository_url") ?? DEFAULT_CONDA_ENDPOINT).EnsureTrailingSlash();

            // Check https://docs.conda.io/projects/conda-build/en/latest/concepts/package-naming-conv.html#index-4
            // for details on Conda naming conventions.
            string fileName = $"{name}-{version}-{build}.{type}";
            return $"{feedUrl}{channel}/{subDir}/{fileName}";
        }
        
        public enum CondaArtifactType
        {
            Unknown = 0,
            TarBz2,
            Conda,
        }
    }
}
