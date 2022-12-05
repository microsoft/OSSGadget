// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Model;
    using Moq;
    using Octokit;
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
    public class MavenProjectManagerTests
    {
        private readonly Mock<MavenProjectManager> _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://repo1.maven.org/maven2/ant/ant/1.6/", Resources.maven_ant_1_6_html },
        }.ToImmutableDictionary();

        public MavenProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();

            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new Mock<MavenProjectManager>(".", new NoOpPackageActions(), _httpFactory) { CallBase = true };
        }

        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6?repository_url=https://repo1.maven.org/maven2", "https://repo1.maven.org/maven2/ant/ant/1.6/")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUriPrefix)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<MavenProjectManager.MavenArtifactType>> uris = await _projectManager.Object.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.Jar
                && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.jar")));
            Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.SourcesJar
                && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}-sources.jar")));
            Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.Pom
                && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.pom")));
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
            httpMock.When(HttpMethod.Get, $"{url}/*.tgz").Respond(statusCode);
        }
    }
}
