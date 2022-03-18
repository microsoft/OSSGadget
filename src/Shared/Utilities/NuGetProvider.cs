// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Utilities;

using Microsoft.CST.OpenSource.Contracts;
using Microsoft.CST.OpenSource.PackageManagers;
using Model;
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
using Repository = NuGet.Protocol.Core.Types.Repository;

public class NuGetProvider : INuGetProvider
{
    private SourceCacheContext _sourceCacheContext = new();
    private SourceRepository _sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
    private NuGetLogger _logger = new(LogManager.GetCurrentClassLogger());

    /// <summary>
    /// Instantiates a new <see cref="NuGetProvider"/>.
    /// </summary>
    public NuGetProvider()
    {
    }

    /// <inheritdoc />
    public async Task<string?> DownloadNupkgAsync(NuGetProjectManager projectManager, PackageURL packageUrl, string targetDirectory, bool doExtract,
        bool cached = false, CancellationToken cancellationToken = default)
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
            return await projectManager.ExtractArchive(targetDirectory, packageStream.ToArray(), cached);
        }

        string filePath = Path.ChangeExtension(targetDirectory, ".nupkg");
        await File.WriteAllBytesAsync(filePath, packageStream.ToArray(), cancellationToken);
        return filePath;
    }

    /// <inheritdoc />
    public async Task<bool> DoesPackageExistAsync(PackageURL packageUrl,
        bool useCache = true, CancellationToken cancellationToken = default)
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
    public async Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl,
        bool useCache = true, CancellationToken cancellationToken = default)
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
    public async Task<NuGetMetadata?> GetMetadataAsync(PackageURL packageUrl,
        bool useCache = true, CancellationToken cancellationToken = default)
    {
        PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
        if (string.IsNullOrWhiteSpace(packageUrl.Version))
        {
            throw new ArgumentNullException(nameof(packageUrl.Version), "There was no version on the PackageURL");
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