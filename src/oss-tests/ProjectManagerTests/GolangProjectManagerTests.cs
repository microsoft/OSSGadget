// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Model;
    using Moq;
    using PackageActions;
    using PackageManagers;
    using PackageUrl;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GolangProjectManagerTests
    {
        private readonly Mock<GolangProjectManager> _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        public GolangProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
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
    }
}
