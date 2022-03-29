// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using Contracts;
using PackageManagers;
using PackageUrl;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class BaseProvider : IManagerProvider
{
    public BaseProvider(IHttpClientFactory? httpClientFactory = null)
    {
        HttpClientFactory = httpClientFactory ?? new DefaultHttpClientFactory();
    }
    
    public BaseProvider() : this(new DefaultHttpClientFactory())
    {}

    /// <inheritdoc />
    public IHttpClientFactory HttpClientFactory { get; protected init; }

    /// <inheritdoc />
    public virtual Task<string?> DownloadAsync(BaseProjectManager projectManager, PackageURL packageUrl, string targetDirectory, bool doExtract,
        bool cached = false, CancellationToken cancellationToken = default) =>
        throw new System.NotImplementedException();

    /// <inheritdoc />
    public virtual Task<bool> DoesPackageExistAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    /// <inheritdoc />
    public virtual Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    /// <inheritdoc />
    public virtual Task<string> GetLatestVersionAsync(PackageURL packageUrl, bool includePrerelease = false, bool useCache = true,
        CancellationToken cancellationToken = default) =>
        throw new System.NotImplementedException();

    /// <inheritdoc />
    public virtual Task<IManagerMetadata?> GetMetadataAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
    
    /// <inheritdoc />
    public virtual HttpClient CreateHttpClient(string? manager = null)
    {
        return HttpClientFactory.CreateClient(manager ?? GetType().Name);
    }
}