// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using Contracts;
using PackageManagers;
using PackageUrl;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class BaseProvider : IManagerProvider<IManagerMetadata>
{
    public Task<string?> DownloadAsync(BaseProjectManager projectManager, PackageURL packageUrl, string targetDirectory, bool doExtract,
        bool cached = false, CancellationToken cancellationToken = default) =>
        throw new System.NotImplementedException();

    public Task<bool> DoesPackageExistAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    public Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    public Task<IManagerMetadata?> GetMetadataAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
}