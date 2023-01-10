// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource.Extensions;
    using Model;
    using Moq;
    using oss;
    using PackageActions;
    using PackageManagers;
    using PackageUrl;
    using RichardSzalay.MockHttp;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GolangProjectManagerTests
    {
        private readonly Mock<GolangProjectManager> _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://proxy.golang.org/sigs.k8s.io/yaml/@v/list", Resources.go_yaml_list },
            { "https://proxy.golang.org/sigs.k8s.io/yaml/@v/v1.3.0.info", Resources.go_yaml_1_3_0_info },
            { "https://proxy.golang.org/sigs.k8s.io/yaml/@v/v1.3.0.mod", Resources.go_yaml_1_3_0_mod },
        }.ToImmutableDictionary();

        public GolangProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new Mock<GolangProjectManager>(".", new NoOpPackageActions(), _httpFactory) { CallBase = true };
        }

        [DataTestMethod]
        [DataRow("pkg:golang/github.com/Azure/go-autorest@v0.11.28#autorest", "https://proxy.golang.org/github.com/azure/go-autorest/autorest/@v/v0.11.28.zip")]
        [DataRow("pkg:golang/sigs.k8s.io/yaml@v1.3.0", "https://proxy.golang.org/sigs.k8s.io/yaml/@v/v1.3.0.zip")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUri)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<GolangProjectManager.GolangArtifactType>> uris = await _projectManager.Object.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            Assert.AreEqual(expectedUri, uris.First().Uri.AbsoluteUri);
            Assert.AreEqual(".zip", uris.First().Extension);
            Assert.AreEqual(GolangProjectManager.GolangArtifactType.Zip, uris.First().Type);
        }

        [DataTestMethod]
        [DataRow("pkg:golang/sigs.k8s.io/yaml@v1.3.0")] // Normal package
        public async Task MetadataSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);
            PackageMetadata? metadata = await _projectManager.Object.GetPackageMetadataAsync(purl, useCache: false);

            Assert.IsNotNull(metadata);
            Assert.AreEqual(purl.GetFullName(), metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.IsNotNull(metadata.UploadTime);
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
        }
    }
}
