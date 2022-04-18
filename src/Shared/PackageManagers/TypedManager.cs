// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers;

using Contracts;
using Extensions;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

/// <summary>
/// An abstract class that implements <see cref="BaseProjectManager"/> that defines an implementation of
/// <see cref="IManagerPackageVersionMetadata"/> to be associated with the manager that implements this class.
/// </summary>
/// <typeparam name="T">
/// The implementation of <see cref="IManagerPackageVersionMetadata"/> for the manager that implements this class.
/// </typeparam>
public abstract class TypedManager<T> : BaseProjectManager where T : IManagerPackageVersionMetadata
{
    /// <summary>
    /// The actions object to be used in the project manager.
    /// </summary>
    protected readonly IManagerPackageActions<T> Actions;

    protected TypedManager(IManagerPackageActions<T> actions, IHttpClientFactory httpClientFactory, string directory) : base(httpClientFactory, directory)
    {
        Actions = actions;
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("DownloadVersion {0}", purl.ToString());

        string fileName = purl.ToStringFilename();
        string targetName = $"{GetType().Name}-{fileName}";
        string? containingPath = await Actions.DownloadAsync(purl, TopLevelExtractionDirectory, targetName, doExtract, cached);

        if (containingPath is string notNullPath)
        {
            if (doExtract)
            {
                return Directory.EnumerateFiles(notNullPath, "*",
                    new EnumerationOptions() {RecurseSubdirectories = true});
            }

            return new[] { containingPath };
        }

        return Array.Empty<string>();
    }

    /// <inheritdoc />
    public override async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("PackageExists {0}", purl.ToString());
        return await Actions.DoesPackageExistAsync(purl, useCache);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<string>> EnumerateVersionsAsync(
        PackageURL purl,
        bool useCache = true,
        bool includePrerelease = true)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("EnumerateVersions {0}", purl.ToString());
        return await Actions.GetAllVersionsAsync(purl, includePrerelease, useCache);
    }

    /// <inheritdoc />
    public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
    {
        return (await Actions.GetMetadataAsync(purl, useCache))?.ToString();
    }
} 