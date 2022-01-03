// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// This is the HttpClientFactory that will be used if one is not specified.  This factory lazily constructs a single HttpClient and presents it for reuse to reduce resource usage.
    /// </summary>
    public sealed class DefaultHttpClientFactory : IHttpClientFactory
    {

        public DefaultHttpClientFactory(string? userAgent)
        {
            _httpClientLazy = new(() =>
            {
                HttpClient cli = new(handler);
                cli.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ?? "OSSDL");
                return cli;
            });
        }

        private static readonly SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = true,
            UseCookies = false,
            MaxAutomaticRedirections = 5,
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        private readonly Lazy<HttpClient> _httpClientLazy;

        /// <summary>
        /// Returns the singleton HttpClient for this factory.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public HttpClient CreateClient(string name) => _httpClientLazy.Value;
    }
}
