// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using PackageManagers;
using PackageUrl;
using System;

public interface IProjectManagerFactory
{
    /// <summary>
    /// Creates an appropriate project manager for a <see cref="PackageURL"/> given its <see cref="PackageURL.Type"/>.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> for the package to create the project manager for.</param>
    /// <param name="destinationDirectory">The new destination directory, if provided.</param>
    /// <param name="timeout">The <see cref="TimeSpan"/> to wait before the request times out.</param>
    /// <param name="allowUseOfRateLimitedRegistryAPIs"> The Cargo Project Manager uses this flag to disable the use of a ratelimited API for 
    /// fetching metadata when the user scenario requires retrieving metadata for more than one package per second. </param>
    /// <returns>The implementation of <see cref="BaseProjectManager"/> for this <paramref name="purl"/>'s type.</returns>
    IBaseProjectManager? CreateProjectManager(PackageURL purl, string destinationDirectory = ".", TimeSpan? timeout = null, bool? allowUseOfRateLimitedRegistryAPIs = true);
}