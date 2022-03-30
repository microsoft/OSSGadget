// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using Contracts;
using Metadata;
using Microsoft.CST.OpenSource.PackageManagers;
using NLog;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

public class NuGetProvider : IManagerProvider
{
    private readonly SourceCacheContext _sourceCacheContext = new();
    private readonly SourceRepository _sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
    private readonly NuGetLogger _logger = new(LogManager.GetCurrentClassLogger());

    /// <summary>
    /// Instantiates a new <see cref="NuGetProvider"/>.
    /// </summary>
    public NuGetProvider()
    {
    }

    /// <inheritdoc />
    public async Task<string?> DownloadAsync(
        PackageURL packageUrl,
        string topLevelDirectory,
        string targetDirectory,
        bool doExtract,
        bool cached = false,
        CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        // Create a new memory stream to populate with the .nupkg.
        using MemoryStream packageStream = new();

        bool downloaded = await resource.CopyNupkgToStreamAsync(
            packageUrl.Name,
            NuGetVersion.Parse(packageUrl.Version),
            packageStream,
            _sourceCacheContext,
            _logger, 
            cancellationToken);
        
        // If the download failed, return null.
        if (!downloaded)
        {
            return null;
        }
        
        // If we want to extract the contents of the .nupkg, send it to ExtractArchive.
        if (doExtract)
        {
            return await BaseProjectManager.ExtractArchive(topLevelDirectory, targetDirectory, packageStream.ToArray(), cached);
        }

        string filePath = Path.ChangeExtension(targetDirectory, ".nupkg");
        await File.WriteAllBytesAsync(filePath, packageStream.ToArray(), cancellationToken);
        return filePath;
    }

    /// <inheritdoc />
    public async Task<bool> DoesPackageExistAsync(
        PackageURL packageUrl,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        // If a version is provided, check for existence of the specified version.
        if (!string.IsNullOrWhiteSpace(packageUrl.Version))
        {
            bool exists = await resource.DoesPackageExistAsync(
                packageUrl.Name,
                NuGetVersion.Parse(packageUrl.Version),
                _sourceCacheContext,
                _logger, 
                cancellationToken);

            return exists;
        }
        
        // If no version is provided, check to see if any versions exist on a package with the given name.
        IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
            packageUrl.Name,
            _sourceCacheContext,
            _logger, 
            cancellationToken);

        return versions.Any();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetAllVersionsAsync(
        PackageURL packageUrl,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
            packageUrl.Name,
            _sourceCacheContext,
            _logger, 
            cancellationToken);

        return versions.Select(v => v.ToString());
    }
    
    /// <inheritdoc />
    public async Task<string> GetLatestVersionAsync(
        PackageURL packageUrl,
        bool includePrerelease = false,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
            packageUrl.Name,
            _sourceCacheContext,
            _logger, 
            cancellationToken);

        return versions.Last().ToString();
    }

    /// <inheritdoc />
    public async Task<IManagerMetadata?> GetMetadataAsync(
        PackageURL packageUrl,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
        if (string.IsNullOrWhiteSpace(packageUrl.Version))
        {
            throw new ArgumentException("There was no version on the PackageURL.", nameof(packageUrl));
        }

        PackageIdentity packageIdentity = new(packageUrl.Name, NuGetVersion.Parse(packageUrl.Version));

        PackageSearchMetadataRegistration? packageVersion = await resource.GetMetadataAsync(
            packageIdentity,
            _sourceCacheContext,
            _logger, 
            cancellationToken) as PackageSearchMetadataRegistration;

        return packageVersion != null ? new NuGetMetadata(packageVersion) : null;
    }
}