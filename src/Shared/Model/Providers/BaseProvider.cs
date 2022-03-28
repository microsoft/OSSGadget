// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using Contracts;
using PackageManagers;
using PackageUrl;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class BaseProvider : IManagerProvider<IManagerMetadata>
{
    public BaseProvider(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    public BaseProvider() : this(new DefaultHttpClientFactory())
    {}

    public IHttpClientFactory HttpClientFactory { get; internal init; }

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
    
    public virtual HttpClient CreateHttpClient()
    {
        return HttpClientFactory.CreateClient(GetType().Name);
    }
}