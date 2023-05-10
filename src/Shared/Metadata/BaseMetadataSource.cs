// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource;

using Helpers;
using System.Threading.Tasks;
using System.Text.Json;
using PackageUrl;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System;
using Polly;
using System.Net;
using Polly.Retry;
using Polly.Contrib.WaitAndRetry;

public abstract class BaseMetadataSource
{
    protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    protected HttpClient HttpClient;
    
    public BaseMetadataSource()
    {
        EnvironmentHelper.OverrideEnvironmentVariables(this);
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider();

        var clientFactory = serviceProvider.GetService<IHttpClientFactory>() ?? throw new InvalidOperationException();
        HttpClient = clientFactory.CreateClient();
    }

    public async Task<JsonDocument?> GetMetadataForPackageUrlAsync(PackageURL packageUrl, bool useCache = false)
    {
        return await GetMetadataAsync(packageUrl.Type, packageUrl.Namespace, packageUrl.Name, packageUrl.Version, useCache);
    }
    public abstract Task<JsonDocument?> GetMetadataAsync(string packageType, string packageNamespace, string packageName, string packageVersion, bool useCache = false);

    /// <summary>
    /// Loads a URL and returns the JSON document, using a retry policy.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to load.</param>
    /// <param name="policy">An optional <see cref="AsyncRetryPolicy"/> to use with the http request.</param>
    /// <returns>The resultant JsonDocument, or an exception on failure.</returns>
    public async Task<JsonDocument> GetJsonWithRetry(string uri, AsyncRetryPolicy<HttpResponseMessage>? policy = null)
    {
        policy ??= Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(15), retryCount: 5));

        var result = await policy.ExecuteAsync(() => HttpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead));
        return await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync());
    }
}