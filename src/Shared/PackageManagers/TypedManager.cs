// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers;

using Contracts;
using Extensions;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class TypedManager<T> : BaseProjectManager where T : IManagerPackageVersionMetadata
{
    protected readonly IManagerPackageActions<T> _actions;

    public TypedManager(IManagerPackageActions<T> actions, string directory) : base(directory)
    {
        _actions = actions;
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("DownloadVersion {0}", purl.ToString());
        string? fileName = purl.ToStringFilename();
        string targetName = $"{GetType().Name}-{fileName}";
        string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
        string? containingPath = await _actions.DownloadAsync(purl, TopLevelExtractionDirectory, extractionPath, doExtract, cached);

        if (containingPath is string notNullPath)
        {
            return Directory.EnumerateFiles(notNullPath, "*",
                new EnumerationOptions() {RecurseSubdirectories = true});
        }

        return Array.Empty<string>();
    }

    /// <inheritdoc />
    public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("PackageExists {0}", purl.ToString());
        return await _actions.DoesPackageExistAsync(purl, useCache);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<string>> EnumerateVersions(
        PackageURL purl,
        bool useCache = true,
        bool includePrerelease = true)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("EnumerateVersions {0}", purl.ToString());
        return await _actions.GetAllVersionsAsync(purl, includePrerelease, useCache);
    }

    /// <inheritdoc />
    public override async Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
    {
        return (await _actions.GetMetadataAsync(purl, useCache)).ToString();
    }
} 