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
using System.Collections.Immutable;
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
        string targetPath,
        bool doExtract,
        bool cached = false,
        CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        // Construct the path for the nupkg file.
        string filePath = Path.Join(topLevelDirectory, targetPath + ".nupkg");

        // Create a new FileStream to populate with the contents of the .nupkg from CopyNupkgToStreamAsync.
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
            return await ArchiveHelper.ExtractArchiveAsync(topLevelDirectory, targetPath + ".nupkg", packageStream, cached);
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
        bool includePrerelease = true,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        IEnumerable<NuGetVersion> versionsAscending = await resource.GetAllVersionsAsync(
            packageUrl.Name,
            _sourceCacheContext,
            _logger, 
            cancellationToken);

        return versionsAscending
            .Where(v => includePrerelease || !v.IsPrerelease)
            .Select(v => v.ToString())
            .Reverse(); // We want the versions in descending order.
    }
    
    /// <inheritdoc />
    public async Task<string?> GetLatestVersionAsync(
        PackageURL packageUrl,
        bool includePrerelease = false,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

        IEnumerable<NuGetVersion> versionsAscending = await resource.GetAllVersionsAsync(
            packageUrl.Name,
            _sourceCacheContext,
            _logger, 
            cancellationToken);

        return versionsAscending
            .LastOrDefault<NuGetVersion?>(v => includePrerelease || (v != null && !v.IsPrerelease)) // The latest version is the last in ascending order.
            ?.ToString();
    }

    /// <inheritdoc />
    public async Task<NuGetPackageVersionMetadata?> GetMetadataAsync(
        PackageURL packageUrl,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
        string? version = packageUrl.Version;
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("There was no version on the PackageURL.", nameof(packageUrl));
        }

        PackageIdentity packageIdentity = new(packageUrl.Name, NuGetVersion.Parse(version));

        PackageSearchMetadataRegistration? packageVersion = await resource.GetMetadataAsync(
            packageIdentity,
            _sourceCacheContext,
            _logger, 
            cancellationToken) as PackageSearchMetadataRegistration;

        return packageVersion != null ? new NuGetPackageVersionMetadata(packageVersion) : null;
    }

    /// <inheritdoc />
    public async Task<bool> GetHasReservedNamespaceAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default)
    {
        PackageSearchResource resource = await _sourceRepository.GetResourceAsync<PackageSearchResource>();

        SearchFilter searchFilter = new(includePrerelease: true);

        IPackageSearchMetadata result = (await resource.SearchAsync(
            packageUrl.Name,
            searchFilter,
            skip: 0,
            take: 1,
            _logger,
            cancellationToken)).First();

        return result.Identity.Id
                   .Equals(packageUrl.Name, StringComparison.InvariantCultureIgnoreCase) &&
               result.PrefixReserved;
    }
}