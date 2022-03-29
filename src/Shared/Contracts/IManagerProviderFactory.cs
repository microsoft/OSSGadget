// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model.Providers;
using PackageUrl;
using System.Net.Http;

public interface IManagerProviderFactory
{
    /// <summary>
    /// The <see cref="IHttpClientFactory"/> to put into any <see cref="IManagerProvider{T}"/> created.
    /// </summary>
    IHttpClientFactory HttpClientFactory { get; }

    /// <summary>
    /// Creates a new provider that inherits <see cref="BaseProvider"/> or returns the <see cref="BaseProvider"/> if none inherit it.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to create a provider for.</param>
    /// <returns>A new <see cref="BaseProvider"/> or class that implements <see cref="BaseProvider"/>.</returns>
    public BaseProvider CreateProvider(PackageURL purl);
}