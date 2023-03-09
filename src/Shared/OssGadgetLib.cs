// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using Helpers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using PackageManagers;
    using System;
    using System.Net.Http;

    public abstract class OssGadgetLib
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_HTTPCLIENT_USER_AGENT { get; set; }= "OSSDL";

        /// <summary>
        /// The <see cref="ProjectManagerFactory"/> to be used by classes that implement <see cref="OssGadgetLib"/>.
        /// </summary>
        protected ProjectManagerFactory ProjectManagerFactory { get; }

        /// <summary>
        /// The <see cref="NLog.ILogger"/> for this class.
        /// </summary>
        protected NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The directory to save files to.
        /// Defaults to the directory the code is running in.
        /// </summary>
        protected string Directory { get; }

        protected OssGadgetLib(ProjectManagerFactory projectManagerFactory, string directory = ".")
        {
            ProjectManagerFactory = Check.NotNull(nameof(projectManagerFactory), projectManagerFactory);
            Directory = directory;
        }

        protected OssGadgetLib(string directory = ".") : this(new ProjectManagerFactory(), directory)
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