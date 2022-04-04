// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageActions;

using Microsoft.CST.OpenSource.Contracts;
using Microsoft.CST.OpenSource.Helpers;
using Microsoft.CST.OpenSource.Model.Metadata;
using Microsoft.CST.OpenSource.Utilities;
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

public class NuGetPackageActions : IManagerPackageActions<NuGetPackageVersionMetadata>
{
    private readonly SourceCacheContext _sourceCacheContext = new();
    private readonly SourceRepository _sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
    private readonly NuGetLogger _logger = new(LogManager.GetCurrentClassLogger());

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

        // Construct the path for the nupkg file.
        string filePath = Path.ChangeExtension(Path.Join(topLevelDirectory, targetDirectory), Path.GetExtension(targetDirectory) + ".nupkg");

        // Create a new memory stream to populate with the .nupkg.
        int bufferSize = 4096;
        await using FileStream packageStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            bufferSize,
            doExtract ? FileOptions.DeleteOnClose : FileOptions.None);
        // If we want to extract the archive, delete the .nupgkg on close, otherwise keep it.

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
        
        // If we want to extract the contents of the .nupkg, send it to ArchiveHelper.ExtractArchiveAsync.
        if (doExtract)
        {
            return await ArchiveHelper.ExtractArchiveAsync(topLevelDirectory, targetDirectory, packageStream, cached);
        }

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
    public async Task<NuGetPackageVersionMetadata?> GetMetadataAsync(
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

        return packageVersion != null ? new NuGetPackageVersionMetadata(packageVersion) : null;
    }
}