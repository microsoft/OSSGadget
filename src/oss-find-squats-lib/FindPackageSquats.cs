// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats
{
    using Microsoft.CST.OpenSource.Lib;
    using System.Net.Http;

    public class FindPackageSquats : OssGadgetLib
    {

        /// <summary>
        /// The <see cref="PackageURL"/> to find squats for..
        /// </summary>
        public string TopLevelExtractionDirectory { get; set; } = ".";

        public FindPackageSquats(IHttpClientFactory httpClientFactory, string directory = ".")
            : base(httpClientFactory, directory)
        {
        }

        private HttpClient CreateHttpClient()
        {
            return this.HttpClientFactory.CreateClient(this.GetType().Name);
        }
    }
}