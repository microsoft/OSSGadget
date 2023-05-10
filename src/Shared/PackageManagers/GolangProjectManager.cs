// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Helpers;
    using Microsoft.CST.OpenSource.Contracts;
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageActions;
    using Microsoft.CST.OpenSource.Utilities;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class GolangProjectManager : TypedManager<IManagerPackageVersionMetadata, GolangProjectManager.GolangArtifactType>
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "golang";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_GO_PROXY_ENDPOINT { get; set; } = "https://proxy.golang.org";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_GO_PKG_ENDPOINT { get; set; } = "https://pkg.go.dev";

        public GolangProjectManager(
            string directory,
            IManagerPackageActions<IManagerPackageVersionMetadata>? actions = null,
            IHttpClientFactory? httpClientFactory = null)
            : base(actions ?? new NoOpPackageActions(), httpClientFactory ?? new DefaultHttpClientFactory(), directory)
        {
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ArtifactUri<GolangArtifactType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true)
        {
            string packageNamespace = Check.NotNull(nameof(purl.Namespace), purl?.Namespace);
            string packageName = Check.NotNull(nameof(purl.Name), purl?.Name);
            string packageVersion = Check.NotNull(nameof(purl.Version), purl?.Version);
            string? packageSubpath = purl?.Subpath;

            // PackageURL normally replaces slashes in the namespace to be commas. This code changes it back to slashes to correctly format the download url.
            string packageSubpathNormalized = !string.IsNullOrWhiteSpace(packageSubpath) ? packageSubpath.ToLowerInvariant() + "/" : string.Empty;
            string artifactUri = $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespace.ToLowerInvariant().Replace(',', '/')}/{packageName.ToLowerInvariant()}/{packageSubpathNormalized}@v/{packageVersion}.zip";
            yield return new ArtifactUri<GolangArtifactType>(GolangArtifactType.Zip, artifactUri);
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true)
        {
            // Packages by owner is not currently supported for Golang, so an empty list is returned.
            yield break;
        }

        /// <summary>
        ///     Download one RubyGems package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageNamespace = purl?.Namespace?.Replace(',', '/');
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            string? packageSubpath = purl?.Subpath;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1} {2}]. All three must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                Uri url = (await GetArtifactDownloadUrisAsync(purl, cached).ToListAsync()).Single().Uri;
                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                string targetName = $"golang-{packageNamespace}-{packageName}-{packageSubpath}@{packageVersion}";
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
                    extractionPath += Path.GetExtension(url.ToString()) ?? "";
                    await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(extractionPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading Go package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name) || string.IsNullOrEmpty(purl.Namespace))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageNamespaceLower = purl.Namespace.ToLowerInvariant().Replace(',', '/');
            string packageNameLower = purl.Name.ToLowerInvariant();
            HttpClient httpClient = CreateHttpClient();

            return await CheckHttpCacheForPackage(httpClient, $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}/@v/list", useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            try
            {
                string? packageNamespaceLower = purl?.Namespace?.ToLowerInvariant()?.Replace(',', '/');
                string? packageNameLower = purl?.Name?.ToLowerInvariant();
                string? packageSubpathLower = purl?.Subpath?.ToLowerInvariant();
                List<string> versionList = new();
                HttpClient httpClient = CreateHttpClient();

                string subPathValue = string.IsNullOrEmpty(packageSubpathLower) ? string.Empty : '/' + packageSubpathLower;
                string? doc = await GetHttpStringCache(httpClient, $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}{subPathValue}/@v/list");
                if (doc != null)
                {
                    foreach (string line in doc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string lineTrim = line.Trim();
                        if (!string.IsNullOrEmpty(lineTrim))
                        {
                            Logger.Debug("Identified {0}/{1} version {2}.", purl?.Namespace, purl?.Name, lineTrim);
                            versionList.Add(line);
                        }
                    }
                }
                else
                {
                    throw new Exception("Invalid response from Go Proxy.");
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
            if (purl is null || purl.Name is null || purl.Namespace is null)
            {
                return null;
            }
            try
            {
                IEnumerable<string> versions = await EnumerateVersionsAsync(purl, useCache);
                if (versions.Any())
                {
                    string latestVersion = versions.First();
                    string packageNamespaceLower = purl.Namespace.ToLowerInvariant().Replace(',', '/');
                    string packageNameLower = purl.Name.ToLowerInvariant();
                    string? packageSubpathLower = purl?.Subpath?.ToLowerInvariant();
                    HttpClient httpClient = CreateHttpClient();

                    string subPathValue = string.IsNullOrEmpty(packageSubpathLower) ? string.Empty : '/' + packageSubpathLower;
                    return await GetHttpStringCache(httpClient, $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}{subPathValue}/@v/{latestVersion}.mod", useCache);
                }
                else
                {
                    throw new Exception("Unable to enumerate verisons.");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching metadata: {0}", ex.Message);
                return null;
            }
        }

        public override async Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true)
        {
            string? content = await GetMetadataAsync(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return null; }

            PackageMetadata metadata = new();
            metadata.Name = purl.GetFullName();
            metadata.PackageVersion = purl?.Version;
            metadata.PackageManagerUri = ENV_GO_PROXY_ENDPOINT.EnsureTrailingSlash();
            metadata.Platform = "Go";
            metadata.Language = "Go";

            string? infoContent = await GetInfoMetadataAsync(purl, useCache);
            if (!string.IsNullOrEmpty(infoContent))
            {
                JsonDocument infoContentJSON = JsonDocument.Parse(infoContent);
                JsonElement root = infoContentJSON.RootElement;

                if (OssUtilities.GetJSONPropertyStringIfExists(root, "Time") is string publishedAtDateString &&
                    !string.IsNullOrWhiteSpace(publishedAtDateString) && DateTime.TryParse(publishedAtDateString, out DateTime publishedAtDate))
                {
                    metadata.UploadTime = publishedAtDate;
                }
            }

            return metadata;
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string packageNamespaceLower = purl.Namespace.ToLowerInvariant().Replace(',', '/');
            string packageNameLower = purl.Name.ToLowerInvariant();
            string? packageSubpathLower = purl?.Subpath?.ToLowerInvariant();
            string subPathValue = string.IsNullOrEmpty(packageSubpathLower) ? string.Empty : '/' + packageSubpathLower;
            return new Uri($"{ENV_GO_PKG_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}{subPathValue}");
        }

        public enum GolangArtifactType
        {
            Unknown = 0,
            Zip,
        }

        private async Task<string?> GetInfoMetadataAsync(PackageURL purl, bool useCache = true)
        {
            if (purl is null || purl.Name is null || purl.Namespace is null || purl.Version is null)
            {
                return null;
            }
            try
            {
                string packageNamespaceLower = purl.Namespace.ToLowerInvariant().Replace(',', '/');
                string packageNameLower = purl.Name.ToLowerInvariant();
                string? packageSubpathLower = purl?.Subpath?.ToLowerInvariant();
                string packageVersion = purl.Version;
                HttpClient httpClient = CreateHttpClient();

                string subPathValue = string.IsNullOrEmpty(packageSubpathLower) ? string.Empty : '/' + packageSubpathLower;
                string? content = await GetHttpStringCache(httpClient, $"{ENV_GO_PROXY_ENDPOINT}/{packageNamespaceLower}/{packageNameLower}{subPathValue}/@v/{packageVersion}.info", useCache);

                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching publish date: {0}", ex.Message);
                return null;
            }
        }
    }
}