// Copyright (c) Microsoft Corporation. Licensed under the MIT License.


namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageActions;
    using Microsoft.CST.OpenSource.PackageManagers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PackageUrl;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    [TestClass]
    public class CondaProjectManagerTests
    {
        private readonly Mock<CondaProjectManager> _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        public CondaProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            _httpFactory = mockFactory.Object;

            _projectManager = new Mock<CondaProjectManager>(".", new NoOpPackageActions(), _httpFactory) { CallBase = true };
        }

        [DataTestMethod]
        [DataRow(
            "pkg:conda/absl-py@0.4.1?build=py36_0&channel=main&subdir=linux-64&type=tar.bz2",
            CondaProjectManager.CondaArtifactType.TarBz2,
            "https://repo.anaconda.com/pkgs/main/linux-64/absl-py-0.4.1-py36_0.tar.bz2")]
        [DataRow(
            "pkg:conda/absl-py@0.4.1?build=py36_0&channel=main&subdir=linux-64&type=conda",
            CondaProjectManager.CondaArtifactType.Conda,
            "https://repo.anaconda.com/pkgs/main/linux-64/absl-py-0.4.1-py36_0.conda")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, CondaProjectManager.CondaArtifactType artifactType, string expectedUri)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<CondaProjectManager.CondaArtifactType>> uris = await _projectManager.Object.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            Assert.AreEqual(expectedUri, uris.First().Uri.AbsoluteUri);
            Assert.AreEqual(artifactType, uris.First().Type);
        }
    }
}
