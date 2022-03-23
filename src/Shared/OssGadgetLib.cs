// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using Contracts;
    using Helpers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using Model.Providers;
    using System;
    using System.Net.Http;

    public abstract class OssGadgetLib
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        protected static string ENV_HTTPCLIENT_USER_AGENT = "OSSDL";

        /// <summary>
        /// The <see cref="IHttpClientFactory"/> to be used by classes that implement <see cref="OssGadgetLib"/>.
        /// </summary>
        protected IHttpClientFactory HttpClientFactory { get; }
        
        /// <summary>
        /// The <see cref="IManagerProvider{IManagerMetadata}"/> to be used by classes that implement <see cref="OssGadgetLib"/>.
        /// </summary>
        protected IManagerProvider<IManagerMetadata> Provider { get; }

        /// <summary>
        /// The <see cref="NLog.ILogger"/> for this class.
        /// </summary>
        protected NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The directory to save files to.
        /// Defaults to the directory the code is running in.
        /// </summary>
        protected string Directory { get; }

        protected OssGadgetLib(IHttpClientFactory httpClientFactory, IManagerProvider<IManagerMetadata>? provider = null, string directory = ".")
        {
            HttpClientFactory = Check.NotNull(nameof(httpClientFactory), httpClientFactory);
            Provider = provider;
            Directory = directory;
        }

        protected OssGadgetLib(string directory = ".") : this(new DefaultHttpClientFactory(), null, directory)
        {
        }

        protected static IHttpClientFactory GetDefaultClientFactory()
        {
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddHttpClient()
                .BuildServiceProvider();

            // Get the IHttpClientFactory
            return serviceProvider.GetService<IHttpClientFactory>() ?? throw new InvalidOperationException();
        }
    }
}