// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Versioning;
using PackageManagers;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Version = System.Version;

/// <summary>
/// The interface to mock for NuGet.Protocol related methods we use.
/// </summary>
public interface INuGetProvider
{
    /// <summary>
    /// Downloads a .nupkg file for a given <see cref="PackageURL"/>, and optionally extracts it.
    /// </summary>
    /// <param name="projectManager">The <see cref="NuGetProjectManager"/> this method is being called from, to use for file extraction.</param>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to download the .nupkg of.</param>
    /// <param name="targetDirectory">The directory to save the contents to.</param>
    /// <param name="doExtract">If the contents of the .nupkg should be extracted.</param>
    /// <param name="cached">If the downloaded contents should be retrieved from the cache if they exist there.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The location of the download, or null if the download failed.</returns>
    Task<string?> DownloadNupkgAsync(NuGetProjectManager projectManager, PackageURL packageUrl, string targetDirectory, bool doExtract, bool cached = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks to see if a package exists. If provided a version, it checks for that specific version.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to check.</param>
    /// <param name="useCache">If the cache should be checked for the existence of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>True if the package is confirmed to exist. False otherwise.</returns>
    Task<bool> DoesPackageExistAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all the versions of a package.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get all the available versions for.</param>
    /// <param name="useCache">If the cache should be checked for the available versions of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="IEnumerable{String}"/> of all of the versions.</returns>
    Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the <see cref="PackageSearchMetadataRegistration"/> for this package version.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/>. Requires a version.</param>
    /// <param name="useCache">If the cache should be checked for the metadata of this package.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to be used in the method call, defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>The <see cref="NuGetMetadata"/> for this package version.</returns>
    /// <exception cref="ArgumentNullException">Thrown if there was no version in packageUrl</exception>
    Task<NuGetMetadata?> GetMetadataAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default);
}