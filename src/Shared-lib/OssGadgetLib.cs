// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Lib
{
    using Helpers;
    using System.Net.Http;

    public abstract class OssGadgetLib
    {
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

        protected OssGadgetLib(IHttpClientFactory httpClientFactory, string directory = ".")
        {
            this.HttpClientFactory = Check.NotNull(nameof(httpClientFactory), httpClientFactory);
            this.Directory = directory;
        }
    }
}