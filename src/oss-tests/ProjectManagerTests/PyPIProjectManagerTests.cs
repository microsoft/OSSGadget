// Copyright (c) Microsoft Corporation. Licensed under the MIT License.


namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Model;
    using Moq;
    using oss;
    using PackageActions;
    using PackageManagers;
    using PackageUrl;
    using RichardSzalay.MockHttp;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PyPIProjectManagerTests
    {
        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://pypi.org/pypi/pandas/json", Resources.pandas_json },
            { "https://pypi.org/pypi/plotly/json", Resources.plotly_json },
            { "https://pypi.org/pypi/requests/json", Resources.requests_json },
        }.ToImmutableDictionary();

        private readonly PyPIProjectManager _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        public PyPIProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            
            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }
 
            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new PyPIProjectManager(".", new NoOpPackageActions(), _httpFactory);
        }

        [Ignore(message: "Ignored until https://github.com/microsoft/OSSGadget/issues/328 is addressed.")]
        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2", "Powerful data structures for data analysis, time series, and statistics")]
        [DataRow("pkg:pypi/plotly@5.7.0", "An open-source, interactive data visualization library for Python")]
        [DataRow("pkg:pypi/requests@2.27.1", "Python HTTP for Humans.")]
        public async Task MetadataSucceeds(string purlString, string? description = null)
        {
            PackageURL purl = new(purlString);
            PackageMetadata metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

            Assert.AreEqual(purl.Name, metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
        }
        
        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2", 86, "1.4.2")]
        [DataRow("pkg:pypi/plotly@3.7.1", 276, "5.7.0")]
        [DataRow("pkg:pypi/requests@2.27.1", 145, "2.27.1")]
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
            List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, useCache: false)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
        }
        
        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2", "https://pypi.org/packages/source/p/pandas/pandas-1.4.2.tar.gz")]
        [DataRow("pkg:pypi/plotly@5.7.0", "https://pypi.org/packages/source/p/plotly/plotly-5.7.0.tar.gz")]
        [DataRow("pkg:pypi/requests@2.27.1", "https://pypi.org/packages/source/r/requests/requests-2.27.1.tar.gz")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUri)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<PyPIProjectManager.PyPIArtifactType>> uris = _projectManager.GetArtifactDownloadUris(purl).ToList();

            Assert.AreEqual(expectedUri, uris.First().Uri.AbsoluteUri);
            Assert.AreEqual(".tar.gz", uris.First().Extension);
            Assert.AreEqual(PyPIProjectManager.PyPIArtifactType.Tarball, uris.First().Type);
            Assert.IsTrue(await _projectManager.UriExistsAsync(uris.First().Uri));
        }
        
        private static void MockHttpFetchResponse(
            HttpStatusCode statusCode,
            string url,
            string content,
            MockHttpMessageHandler httpMock)
        {
            httpMock
                .When(HttpMethod.Get, url)
                .Respond(statusCode, "application/json", content);

            if (url.EndsWith("/json"))
            {
                string newUrlNoJson = url.Substring(0, url.Length - "/json".Length);

                int pos = newUrlNoJson.LastIndexOf("/", StringComparison.Ordinal) + 1;
                string packageName = newUrlNoJson.Substring(pos, newUrlNoJson.Length - pos).ToLowerInvariant();

                // Mock the call to get the artifact tarball.
                httpMock
                    .When(HttpMethod.Get,
                        $"https://pypi.org/packages/source/{packageName[0]}/{packageName}/{packageName}-*.tar.gz")
                    .Respond(statusCode);
            }
        }
    }
}
