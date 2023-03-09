// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using Microsoft.CST.OpenSource.Helpers;
    using System;
    using System.Net.Http;

    /// <summary>
    /// This is the HttpClientFactory that will be used if one is not specified.  This factory lazily constructs a single HttpClient and presents it for reuse to reduce resource usage.
    /// </summary>
    public sealed class DefaultHttpClientFactory : IHttpClientFactory
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_HTTPCLIENT_USER_AGENT { get; set; } = "microsoft_oss_gadget (https://github.com/microsoft/OSSGadget)";

        public DefaultHttpClientFactory(string? userAgent = null)
        {
            EnvironmentHelper.OverrideEnvironmentVariables(this);

            _httpClientLazy = new(() =>
            {
                HttpClient cli = new(handler);
                cli.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ?? ENV_HTTPCLIENT_USER_AGENT);
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
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions() {
                CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.Online
            }
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
