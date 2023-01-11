// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageActions;
    using Moq;
    using oss;
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
    public class CargoProjectManagerTests
    {
        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://raw.githubusercontent.com/rust-lang/crates.io-index/master/ra/nd/rand", Resources.cargo_rand },
            { "https://crates.io/api/v1/crates/rand", Resources.cargo_rand_json },
        }.ToImmutableDictionary();

        private readonly CargoProjectManager _projectManager;
        private readonly IHttpClientFactory _httpFactory;
        
        public CargoProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            
            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.8.5", "https://crates.io/api/v1/crates/rand/0.8.5/download")]
        [DataRow("pkg:cargo/quote@1.0.21", "https://crates.io/api/v1/crates/quote/1.0.21/download")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUri)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<CargoProjectManager.CargoArtifactType>> uris = await _projectManager.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            Assert.AreEqual(expectedUri, uris.First().Uri.AbsoluteUri);
            Assert.AreEqual(CargoProjectManager.CargoArtifactType.Tarball, uris.First().Type);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3", 68, "0.8.5")]
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
            List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, useCache: false)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3")]
        public async Task PackageVersionExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsTrue(await _projectManager.PackageVersionExistsAsync(purl, useCache: false));
        }
        
        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.4")]
        public async Task PackageVersionDoesntExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsFalse(await _projectManager.PackageVersionExistsAsync(purl, useCache: false));
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3")] // Normal package
        public async Task MetadataSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);
            PackageMetadata? metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

            Assert.IsNotNull(metadata);
            Assert.AreEqual(purl.GetFullName(), metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.IsNotNull(metadata.UploadTime);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.4")] // Does Not Exist package
        public async Task MetadataFails(string purlString)
        {
            PackageURL purl = new(purlString);
            PackageMetadata? metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

            Assert.IsNull(metadata);
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
