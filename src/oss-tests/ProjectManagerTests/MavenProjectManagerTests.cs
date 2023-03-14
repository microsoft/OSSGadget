// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource.Extensions;
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
            { "https://repo1.maven.org/maven2/ant/ant/", Resources.maven_ant_all_html },
            { "https://repo1.maven.org/maven2/ant/ant/maven-metadata.xml", Resources.maven_ant_metadata_xml },
            { "https://repo1.maven.org/maven2/ant/ant/1.6/ant-1.6.pom", Resources.maven_ant_1_6_pom },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/0.0.8/", Resources.maven_microsoft_fluentui_listitem_0_0_8_html },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/", Resources.maven_microsoft_fluentui_listitem_all_html },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/maven-metadata.xml", Resources.maven_microsoft_fluentui_listitem_metadata_xml },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/0.0.8/fluentui_listitem-0.0.8.pom", Resources.maven_fluentui_listitem_0_0_8_pom },
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

        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6?repository_url=https://repo1.maven.org/maven2")] // Normal package
        public async Task MetadataSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);
            PackageMetadata? metadata = await _projectManager.Object.GetPackageMetadataAsync(purl, useCache: false);

            Assert.IsNotNull(metadata);
            Assert.AreEqual(purl.GetFullName(), metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.IsNotNull(metadata.UploadTime);
        }
        
        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6", 13, "1.7.0")]
        [DataRow("pkg:maven/com.microsoft.fluentui/fluentui_listitem@0.0.8", 21, "0.1.6")]
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
            List<string> versions = (await _projectManager.Object.EnumerateVersionsAsync(purl, useCache: false)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
        }
        
        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6")]
        [DataRow("pkg:maven/com.microsoft.fluentui/fluentui_listitem@0.0.8")]
        public async Task PackageVersionExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsTrue(await _projectManager.Object.PackageVersionExistsAsync(purl, useCache: false));
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
