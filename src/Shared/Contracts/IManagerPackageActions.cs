// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using PackageUrl;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The interface to implement project manager specific actions.
/// </summary>
public interface IManagerPackageActions<T> where T : IManagerPackageVersionMetadata
{
    /// <summary>
    /// Downloads the file(s) associated with a given <see cref="PackageURL"/>, and optionally extracts it if downloading an archive.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to download the contents of.</param>
    /// <param name="topLevelDirectory">The top level directory to create <paramref name="targetPath"/> in.</param>
    /// <param name="targetPath">The path to save the contents to, within the <paramref name="topLevelDirectory"/>.</param>
    /// <param name="doExtract">If the contents of the archive should be extracted.</param>
    /// <param name="cached">If the downloaded contents should be retrieved from the cache if they exist there.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>
    /// The location of the downloaded file, or the location of the directory if extracted.
    /// Otherwise returns null if the download failed.
    /// </returns>
    Task<string?> DownloadAsync(PackageURL packageUrl, string topLevelDirectory, string targetPath, bool doExtract, bool cached = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks to see if a package exists. If provided a version, it checks for existence of that specific version only.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to check.</param>
    /// <param name="useCache">If the cache should be checked for the existence of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>True if the package is confirmed to exist. False otherwise.</returns>
    Task<bool> DoesPackageExistAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all versions of a package, both releases and pre-releases are included by default.
    /// </summary>
    /// <remarks>The first version in the list is the latest, and the oldest version is the last.</remarks>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get all available versions for.</param>
    /// <param name="includePrerelease">If pre-release/beta versions should be included, defaults to <c>true</c>.</param>
    /// <param name="useCache">If the cache should be checked for the available versions of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="IEnumerable{String}"/> of all of the versions for this package, in descending order.</returns>
    /// <remarks>
    /// If <paramref name="includePrerelease"/> is <c>false</c>, and every version is a pre-release, this method will return an empty list.
    /// </remarks>
    Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl, bool includePrerelease = true, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest version of a package, both releases and pre-releases are included by default.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get the latest version for.</param>
    /// <param name="includePrerelease">If pre-release/beta versions should be included, defaults to <c>true</c>.</param>
    /// <param name="useCache">If the cache should be checked for the latest of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The latest version of the <paramref name="packageUrl"/> or null if there are no versions.</returns>
    /// <remarks>
    /// If <paramref name="includePrerelease"/> is <c>false</c>, and every version is a pre-release, this method will return <c>null</c>.
    /// </remarks>
    Task<string?> GetLatestVersionAsync(PackageURL packageUrl, bool includePrerelease = false, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the <see cref="T"/> for this package version.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/>. Requires a version.</param>
    /// <param name="useCache">If the cache should be checked for the metadata of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The <see cref="T"/> for this package version. Or null if none was found.</returns>
    /// <exception cref="ArgumentException">Thrown if there was no version in <paramref name="packageUrl"/>.</exception>
    Task<T?> GetMetadataAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets if the package is in a reserved namespace.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/>.</param>
    /// <param name="useCache">If the cache should be checked if the package is in a reserved namespace.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>True if the package is verified to be in a reserved namespace, false if not.</returns>
    Task<bool> GetHasReservedNamespaceAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);
} 