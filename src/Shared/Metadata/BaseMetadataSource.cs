// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource;

using System.Threading.Tasks;
using System.Text.Json;
using PackageUrl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System;

public abstract class BaseMetadataSource
{
    protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    protected HttpClient HttpClient;
    
    public BaseMetadataSource()
    {
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider();

        var clientFactor = serviceProvider.GetService<IHttpClientFactory>() ?? throw new InvalidOperationException();
        HttpClient = clientFactor.CreateClient();
    }

    public async Task<JsonDocument?> GetMetadataForPackageUrlAsync(PackageURL packageUrl, bool useCache = false)
    {
        return await GetMetadataAsync(packageUrl.Type, packageUrl.Namespace, packageUrl.Name, packageUrl.Version, useCache);
    }
    public abstract Task<JsonDocument?> GetMetadataAsync(string packageType, string packageNamespace, string packageName, string packageVersion, bool useCache = false);
}