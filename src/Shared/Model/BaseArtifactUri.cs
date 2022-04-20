// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

public abstract record BaseArtifactUri(Enum Type, Uri Uri, string Extension)
{
    /// <summary>
    /// The enum representing the artifact type for the project manager associated with this artifact.
    /// </summary>
    public Enum Type { get; } = Type;

    /// <summary>
    /// The <see cref="Uri"/> for where this artifact can be found online.
    /// </summary>
    public Uri Uri { get; } = Uri;

    /// <summary>
    /// The file extension for this artifact file.
    /// </summary>
    public string Extension { get; } = Extension;

    /// <summary>
    /// Check to see if the <see cref="Uri"/> exists.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use when checking if the <see cref="Uri"/> exists.</param>
    /// <param name="policy">An optional <see cref="AsyncRetryPolicy"/> to use with the http request.</param>
    /// <returns>If the request returns <see cref="HttpStatusCode.OK"/>.</returns>
    public async Task<bool> ExistsAsync(HttpClient httpClient, AsyncRetryPolicy<HttpResponseMessage>? policy = null)
    {
        policy ??= DefaultPolicy;

        return (await policy.ExecuteAsync(() => httpClient.GetAsync(Uri, HttpCompletionOption.ResponseHeadersRead))).StatusCode == HttpStatusCode.OK;
    }

    /// <summary>
    /// The delay to use in the <see cref="DefaultPolicy"/>.
    /// </summary>
    private static readonly IEnumerable<TimeSpan> Delay =
        Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(100), retryCount: 5);

    /// <summary>
    /// The default policy to use when checking to see existence of the <see cref="Uri"/>.
    /// </summary>
    private static readonly AsyncRetryPolicy<HttpResponseMessage> DefaultPolicy =
        Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => r.StatusCode != HttpStatusCode.OK) // Also consider any response that doesn't have a 200 status code to be a failure.
            .WaitAndRetryAsync(Delay);
}