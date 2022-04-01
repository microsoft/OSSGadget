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
public interface IManagerPackageActions
{
    /// <summary>
    /// Downloads the file(s) associated with a given <see cref="PackageURL"/>, and optionally extracts it if downloading an archive.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to download the contents of.</param>
    /// <param name="topLevelDirectory">The top level directory to create <paramref name="targetDirectory"/> in.</param>
    /// <param name="targetDirectory">The directory to save the contents to.</param>
    /// <param name="doExtract">If the contents of the archive should be extracted.</param>
    /// <param name="cached">If the downloaded contents should be retrieved from the cache if they exist there.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The location of the download, or null if the download failed.</returns>
    Task<string?> DownloadAsync(PackageURL packageUrl, string topLevelDirectory, string targetDirectory, bool doExtract, bool cached = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks to see if a package exists. If provided a version, it checks for existence of that specific version.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to check.</param>
    /// <param name="useCache">If the cache should be checked for the existence of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>True if the package is confirmed to exist. False otherwise.</returns>
    Task<bool> DoesPackageExistAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all versions of a package.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get all available versions for.</param>
    /// <param name="useCache">If the cache should be checked for the available versions of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="IEnumerable{String}"/> of all of the versions for this package.</returns>
    Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest version of a package.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get the latest version for.</param>
    /// <param name="includePrerelease">If pre-release/beta versions should be included.</param>
    /// <param name="useCache">If the cache should be checked for the latest of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The latest version of the <paramref name="packageUrl"/>.</returns>
    Task<string> GetLatestVersionAsync(PackageURL packageUrl, bool includePrerelease = false, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the <see cref="T"/> for this package version.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/>. Requires a version.</param>
    /// <param name="useCache">If the cache should be checked for the metadata of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The <see cref="T"/> for this package version. Or null if none was found.</returns>
    /// <exception cref="ArgumentException">Thrown if there was no version in <paramref name="packageUrl"/>.</exception>
    Task<IManagerPackageVersionMetadata?> GetMetadataAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);
} 