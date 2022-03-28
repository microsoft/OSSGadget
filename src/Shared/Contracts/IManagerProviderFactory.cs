// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model.Providers;
using PackageUrl;
using System.Net.Http;

public interface IManagerProviderFactory
{
    public IHttpClientFactory HttpClientFactory { get; }

    public BaseProvider CreateProvider(PackageURL purl);
}