// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageActions;

using Contracts;
using PackageUrl;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class NoOpPackageActions : IManagerPackageActions<IManagerPackageVersionMetadata>
{
    public Task<string?> DownloadAsync(
        PackageURL packageUrl,
        string topLevelDirectory,
        string targetPath,
        bool doExtract,
        bool cached = false,
        CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task<bool> DoesPackageExistAsync(
        PackageURL packageUrl,
        bool useCache = true,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<IEnumerable<string>> GetAllVersionsAsync(
        PackageURL packageUrl,
        bool includePrerelease = true,
        bool useCache = true,
        CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<string>());

    public Task<string?> GetLatestVersionAsync(
        PackageURL packageUrl,
        bool includePrerelease = false,
        bool useCache = true,
        CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task<IManagerPackageVersionMetadata?> GetMetadataAsync(
        PackageURL packageUrl,
        bool useCache = true,
        CancellationToken cancellationToken = default) => Task.FromResult<IManagerPackageVersionMetadata?>(null);

    public Task<bool> GetHasReservedNamespaceAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => Task.FromResult(false);
}