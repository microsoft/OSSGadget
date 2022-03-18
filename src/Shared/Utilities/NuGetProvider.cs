// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Utilities;

using Microsoft.CST.OpenSource.Contracts;
using Microsoft.CST.OpenSource.PackageManagers;
using NLog;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using PackageUrl;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class NuGetProvider : INuGetProvider
{
    private SourceCacheContext _sourceCacheContext = new();
    private SourceRepository _sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
    private NuGetLogger _logger;

    /// <summary>
    /// Instantiates a new <see cref="NuGetProvider"/>.
    /// </summary>
    /// <param name="logger">The <see cref="NLog.Logger"/> to use as a <see cref="NuGet.Common.ILogger"/></param>
    public NuGetProvider(Logger logger)
    {
        _logger = new NuGetLogger(logger);
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
    public async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(PackageURL packageUrl,
        bool useCache = true, CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
            packageUrl.Name,
            _sourceCacheContext,
            _logger, 
            cancellationToken);

        return versions;
    }

    /// <inheritdoc />
    public async Task<PackageSearchMetadataRegistration?> GetMetadataAsync(PackageIdentity packageIdentity,
        bool useCache = true, CancellationToken cancellationToken = default)
    {
        PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();

        PackageSearchMetadataRegistration? packageVersion = await resource.GetMetadataAsync(
            packageIdentity,
            _sourceCacheContext,
            _logger, 
            cancellationToken) as PackageSearchMetadataRegistration;

        return packageVersion;
    }
}