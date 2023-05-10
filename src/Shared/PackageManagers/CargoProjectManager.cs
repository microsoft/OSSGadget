// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Extensions;
    using Helpers;
    using Microsoft.CST.OpenSource.Contracts;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageActions;
    using Microsoft.CST.OpenSource.Utilities;
    using Model.Enums;
    using Octokit;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class CargoProjectManager : TypedManager<IManagerPackageVersionMetadata, CargoProjectManager.CargoArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "cargo";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_CARGO_ENDPOINT { get; set; } = "https://crates.io";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_CARGO_ENDPOINT_STATIC { get; set; } = "https://static.crates.io";
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_CARGO_INDEX_ENDPOINT { get; set; } = "https://raw.githubusercontent.com/rust-lang/crates.io-index/master";

        public CargoProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory)
        {
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ArtifactUri<CargoArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;

            string artifactUri = $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}/{packageVersion}/download";
            yield return new ArtifactUri<CargoArtifactType>(CargoArtifactType.Tarball, artifactUri);
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true)
        {
            // Packages by owner is not currently supported for Cargo, so an empty list is returned.
            yield break;
        }

        /// <summary>
        ///     Download one Cargo package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> Path to the downloaded package </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            string? fileName = purl?.ToStringFilename();
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion) || string.IsNullOrWhiteSpace(fileName))
            {
                Logger.Debug("Error with 'purl' argument. Unable to download [{0} {1}] @ {2}. Both must be defined.", packageName, packageVersion, fileName);
                return downloadedPaths;
            }

            Uri url = (await GetArtifactDownloadUrisAsync(purl, cached).ToListAsync()).Single().Uri;
            try
            {
                string targetName = $"cargo-{fileName}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                // if the cache is already present, no need to extract
                if (doExtract && cached && Directory.Exists(extractionPath))
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }
                Logger.Debug("Downloading {0}", url);

                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();

                if (doExtract)
                {
                    downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsStreamAsync(), cached));
                }
                else
                {
                    extractionPath += Path.GetExtension(url.ToString()) ?? "";
                    await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(extractionPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading Cargo package: {0}", ex.Message);
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
            // NOTE: The file isn't valid json, so use the custom rule.
            return await CheckJsonCacheForPackage(httpClient, $"{ENV_CARGO_INDEX_ENDPOINT}/{CreatePath(packageName)}", useCache: useCache, jsonParsingOption: JsonParsingOption.NotInArrayNotCsv);
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
                string? packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();
                // NOTE: The file isn't valid json, so use the custom rule.
                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_CARGO_INDEX_ENDPOINT}/{CreatePath(packageName)}", jsonParsingOption: JsonParsingOption.NotInArrayNotCsv);
                List<string> versionList = new();
                foreach (JsonElement versionObject in doc.RootElement.EnumerateArray())
                {
                    if (versionObject.TryGetProperty("vers", out JsonElement version))
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, version.ToString());
                        if (version.ToString() is string s)
                        {
                            versionList.Add(s);
                        }
                    }
                }
                return SortVersions(versionList.Distinct());
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

        /// <inheritdoc />
        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                string? packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();
                string? content = await GetHttpStringCache(httpClient, $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}", useCache);
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching Cargo metadata: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true)
        {
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return null; }

            PackageMetadata metadata = new();
            metadata.Name = purl?.Name;
            metadata.PackageVersion = purl?.Version;
            metadata.PackageManagerUri = ENV_CARGO_ENDPOINT.EnsureTrailingSlash();
            metadata.Platform = "Cargo";
            metadata.Language = "Rust";

            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;
            JsonElement? crateElement = OssUtilities.GetJSONPropertyIfExists(root, "crate");

            if (crateElement != null)
            {
                if (OssUtilities.GetJSONPropertyStringIfExists(crateElement, "description") is string description &&
                        !string.IsNullOrWhiteSpace(description))
                {
                    metadata.Description = description;
                }

                if (OssUtilities.GetJSONPropertyStringIfExists(crateElement, "newest_version") is string newestVersion &&
                        !string.IsNullOrWhiteSpace(newestVersion))
                {
                    metadata.LatestPackageVersion = newestVersion;
                }
            }

            JsonElement.ArrayEnumerator? versionsListElement = OssUtilities.GetJSONEnumerator(OssUtilities.GetJSONPropertyIfExists(root, "versions"));
            if (versionsListElement != null)
            {
                JsonElement targetVersionElement = versionsListElement.Value
                    .Where(versionElement =>
                    {
                        return OssUtilities.GetJSONPropertyStringIfExists(versionElement, "num") is string versionNumber && versionNumber == purl?.Version;
                    }).SingleOrDefault();

                // The specified version does not exist. Return null for the method.
                if (targetVersionElement.Equals(default(JsonElement)))
                {
                    return null;
                }

                if (OssUtilities.GetJSONPropertyStringIfExists(targetVersionElement, "created_at") is string createdAtString &&
                    !string.IsNullOrWhiteSpace(createdAtString) && DateTime.TryParse(createdAtString, out DateTime createdAtDate))
                {
                    metadata.UploadTime = createdAtDate;
                }
            }

            return metadata;
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            string url = $"{ENV_CARGO_ENDPOINT}/crates/{packageName}";
            if (packageVersion.IsNotBlank())
            {
                url += $"/{packageVersion}";
            }
            return new Uri(url);
        }
        
        /// <summary>
        /// Helper method to create the path for the crates.io index for this package name.
        /// </summary>
        /// <param name="crateName">The name of this package.</param>
        /// <returns>The path to the package.</returns>
        /// <example>
        /// rand -> ra/nd/rand<br/>
        /// go -> 2/go<br/>
        /// who -> 3/w/who<br/>
        /// spotify-retro -> sp/ot/spotify-retro
        /// </example>
        public static string CreatePath(string crateName)
        {
            switch (crateName.Length)
            {
                case 0:
                    return string.Empty;
                case 1:
                    return $"1/{crateName}";
                case 2:
                    return $"2/{crateName}";
                case 3:
                    return $"3/{crateName[0]}/{crateName}";
                default:
                    return $"{crateName[..2]}/{crateName[2..4]}/{crateName}";
            }
        }

        public enum CargoArtifactType
        {
            Unknown = 0,
            Tarball,
        }
    }
}