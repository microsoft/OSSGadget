// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Version = SemanticVersioning.Version;

public interface IBaseProjectManager
{
    /// <summary>
    /// The type of the project manager from the package-url type specifications.
    /// </summary>
    /// <remarks>This differs from the Type property defined in other ProjectManagers as this one isn't static.</remarks>
    /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
    public string ManagerType { get; }

    /// <summary>
    /// Per-object option container.
    /// </summary>
    public Dictionary<string, object> Options { get; }

    /// <summary>
    /// The location (directory) to extract files to.
    /// </summary>
    public string TopLevelExtractionDirectory { get; init; }

    /// <summary>
    /// The <see cref="IHttpClientFactory"/> for the manager.
    /// </summary>
    public IHttpClientFactory HttpClientFactory { get; }

    /// <summary>
    /// Downloads a given PackageURL and extracts it locally to a directory.
    /// </summary>
    /// <param name="purl">PackageURL to download</param>
    /// <returns>Paths (either files or directory names) pertaining to the downloaded files.</returns>
    public Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false);

    /// <summary>
    /// Enumerates all possible versions of the package identified by purl, in descending order.
    /// </summary>
    /// <remarks>The latest version is always first, then it is sorted by SemVer in descending order.</remarks>
    /// <param name="purl">Package URL specifying the package. Version is ignored.</param>
    /// <param name="useCache">If the cache should be used when looking for the versions.</param>
    /// <param name="includePrerelease">If pre-release versions should be included.</param>
    /// <returns> A list of package version numbers.</returns>
    public Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true);

    /// <summary>
    /// Gets the latest version from the package metadata.
    /// </summary>
    /// <param name="metadata">The package metadata to parse.</param>
    /// <returns>The latest version of the package.</returns>
    public Version? GetLatestVersion(JsonDocument metadata);

    /// <summary>
    /// Check if the package exists in the repository.
    /// </summary>
    /// <param name="purl">The PackageURL to check.</param>
    /// <param name="useCache">If the cache should be checked for the existence of this package.</param>
    /// <returns>True if the package is confirmed to exist in the repository. False otherwise.</returns>
    public Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true);

    /// <summary>
    /// Check if the package exists/existed in the repository.
    /// </summary>
    /// <param name="purl">The PackageURL to check.</param>
    /// <param name="useCache">If the cache should be checked for the existence of this package.</param>
    /// <returns>A <see cref="IPackageExistence"/> detailing the existence of the package.</returns>
    public Task<IPackageExistence> DetailedPackageExistsAsync(PackageURL purl, bool useCache = true);

    /// <summary>
    /// Check if the package version exists in the repository.
    /// </summary>
    /// <param name="purl">The PackageURL to check, requires a version.</param>
    /// <param name="useCache">If the cache should be checked for the existence of this package.</param>
    /// <returns>True if the package version is confirmed to exist in the repository. False otherwise.</returns>
    public Task<bool> PackageVersionExistsAsync(PackageURL purl, bool useCache = true);

    /// <summary>
    /// Check if the package version exists/existed in the repository.
    /// </summary>
    /// <param name="purl">The PackageURL to check.</param>
    /// <param name="useCache">If the cache should be checked for the existence of this package version.</param>
    /// <returns>A <see cref="IPackageExistence"/> detailing the existence of the package version.</returns>
    public Task<IPackageExistence> DetailedPackageVersionExistsAsync(PackageURL purl, bool useCache = true);

    /// <summary>
    /// This method should return text reflecting metadata for the given package. There is no
    /// assumed format.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to get the metadata for.</param>
    /// <param name="useCache">If the metadata should be retrieved from the cache, if it is available.</param>
    /// <returns>A string representing the <see cref="PackageURL"/>'s metadata, or null if it wasn't found.</returns>
    public Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true);

    /// <summary>
    /// Get the uri for the package home page (no version)
    /// </summary>
    /// <param name="purl"></param>
    /// <returns></returns>
    public Uri? GetPackageAbsoluteUri(PackageURL purl);

    /// <summary>
    /// Return a normalized package metadata.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to get the normalized metadata for.</param>
    /// <param name="includePrerelease">If pre-releases should count for getting the latest version, and the list of versions. Defaults to <c>false</c>.</param>
    /// <param name="useCache">If the <see cref="PackageMetadata"/> should be retrieved from the cache, if it is available.</param>
    /// <remarks>If no version specified, defaults to latest version.</remarks>
    /// <returns>A <see cref="PackageMetadata"/> object representing this <see cref="PackageURL"/>.</returns>
    public Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true);

    /// <summary>
    /// Gets everything contained in a JSON element for the package version
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public JsonElement? GetVersionElement(JsonDocument contentJSON, Version version);

    /// <summary>
    /// Gets all the versions of a package
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public List<Version> GetVersions(JsonDocument? metadata);

    /// <summary>
    /// Tries to find out the package repository from the metadata of the package. Check with
    /// the specific package manager, if they have any specific extraction to do, w.r.t the
    /// metadata. If they found some package specific well defined metadata, use that. If that
    /// doesn't work, do a search across the metadata to find probable source repository urls
    /// </summary>
    /// <param name="purl">PackageURL to search</param>
    /// <param name="useCache">If the source repository should be returned from the cache, if available.</param>
    /// <returns>
    /// A dictionary, mapping each possible repo source entry to its probability/empty dictionary
    /// </returns>
    public Task<Dictionary<PackageURL, double>> IdentifySourceRepositoryAsync(PackageURL purl, bool useCache = true);

    /// <summary>
    /// Gets the <see cref="DateTime"/> a package version was published at.
    /// </summary>
    /// <param name="purl">Package URL specifying the package. Version is mandatory.</param>
    /// <param name="useCache">If the cache should be used when looking for the published time.</param>
    /// <returns>The <see cref="DateTime"/> when this version was published, or null if not found.</returns>
    public Task<DateTime?> GetPublishedAtUtcAsync(PackageURL purl, bool useCache = true);
}