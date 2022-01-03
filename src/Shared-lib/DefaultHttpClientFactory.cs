// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using System;
    using System.Net.Http;

    public sealed class DefaultHttpClientFactory : IHttpClientFactory
    {            // Initialize the static HttpClient
        private static SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = true,
            UseCookies = false,
            MaxAutomaticRedirections = 5,
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        private static readonly Lazy<HttpClient> _httpClientLazy =
            new Lazy<HttpClient>(() => new HttpClient(handler));

        public HttpClient CreateClient(string name) => _httpClientLazy.Value;
    }
}
