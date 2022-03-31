// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using Helpers;
    using Microsoft.Extensions.DependencyInjection;
    using PackageManagers;
    using System;
    using System.Net.Http;

    public abstract class OssGadgetLib
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        protected static string ENV_HTTPCLIENT_USER_AGENT = "OSSDL";

        /// <summary>
        /// The <see cref="ProjectManagerFactory"/> to be used by classes that implement <see cref="OssGadgetLib"/>.
        /// </summary>
        protected ProjectManagerFactory ProjectManagerFactory { get; }

        /// <summary>
        /// The <see cref="IHttpClientFactory"/> to be used by classes that implement <see cref="OssGadgetLib"/>.
        /// </summary>
        protected IHttpClientFactory HttpClientFactory { get; }

        /// <summary>
        /// The <see cref="NLog.ILogger"/> for this class.
        /// </summary>
        protected NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// The directory to save files to.
        /// Defaults to the directory the code is running in.
        /// </summary>
        protected string Directory { get; }

        protected OssGadgetLib(ProjectManagerFactory projectManagerFactory, IHttpClientFactory httpClientFactory, string directory = ".")
        {
            ProjectManagerFactory = Check.NotNull(nameof(projectManagerFactory), projectManagerFactory);
            HttpClientFactory = Check.NotNull(nameof(httpClientFactory), httpClientFactory);
            Directory = directory;
        }

        protected OssGadgetLib(IHttpClientFactory httpClientFactory, string directory = ".") : this(new ProjectManagerFactory(httpClientFactory), httpClientFactory, directory)
        {
        }
        
        protected OssGadgetLib(string directory = ".") : this(new ProjectManagerFactory(), new DefaultHttpClientFactory(), directory)
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