// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Model.Enums;
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
    using System.Text;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MavenProjectManagerTests
    {
        public const string DEFAULT_MAVEN_ENDPOINT = "https://repo1.maven.org/maven2";
        public const string GOOGLE_MAVEN_ENDPOINT = "https://maven.google.com";

        private readonly Mock<MavenProjectManager> _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://repo1.maven.org/maven2/ant/ant/", Resources.maven_ant_all_html },
            { "https://repo1.maven.org/maven2/ant/ant/maven-metadata.xml", Resources.maven_ant_metadata_xml },
            { "https://repo1.maven.org/maven2/ant/ant/1.6/", Resources.maven_ant_1_6_html },
            { "https://repo1.maven.org/maven2/ant/ant/1.6/ant-1.6.pom", Resources.maven_ant_1_6_pom },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/", Resources.maven_fluentui_listitem_all_html },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/maven-metadata.xml", Resources.maven_fluentui_listitem_metadata_xml },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/0.0.8/", Resources.maven_fluentui_listitem_0_0_8_html },
            { "https://repo1.maven.org/maven2/com/microsoft/fluentui/fluentui_listitem/0.0.8/fluentui_listitem-0.0.8.pom", Resources.maven_fluentui_listitem_0_0_8_pom },
            { "https://maven.google.com/android/arch/core/core/maven-metadata.xml", Resources.maven_core_metadata_xml },
            { "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2.aar", Resources.maven_core_1_0_0_alpha2_aar },
            { "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2.pom", Resources.maven_core_1_0_0_alpha2_pom },
            { "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2-sources.jar", Resources.maven_core_1_0_0_alpha2_sources_jar },
            { "https://maven.google.com/com/google/cose/cose/maven-metadata.xml", Resources.maven_cose_metadata_xml },
            { "https://maven.google.com/com/google/cose/cose/20230908/cose-20230908.pom", Resources.maven_cose_20230908_pom },
            { "https://maven.google.com/com/google/cose/cose/20230908/cose-20230908.jar", Resources.maven_cose_20230908_jar },
            { "https://maven.google.com/com/google/cose/cose/20230908/cose-20230908-javadoc.jar", Resources.maven_cose_20230908_javadoc_jar },
            { "https://maven.google.com/com/google/cose/cose/20230908/artifact-metadata.json", Resources.maven_cose_20230908_artifact_metadata_json },
        }.ToImmutableDictionary();

        public MavenProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();

            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2-client.jar").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2.ear").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2-javadoc.jar").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2.rar").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2-tests.jar").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2-tests-sources.jar").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2.war").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/core-1.0.0-alpha2.jar").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/android/arch/core/core/").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://maven.google.com/com/google/cose/cose/").Respond(HttpStatusCode.NotFound);

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new Mock<MavenProjectManager>(".", new NoOpPackageActions(), _httpFactory, null) { CallBase = true };
        }

        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6?repository_url=https://repo1.maven.org/maven2", "https://repo1.maven.org/maven2/ant/ant/1.6/")]
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha2?repository_url=https://maven.google.com", "https://maven.google.com/android/arch/core/core/1.0.0-alpha2/")]
        [DataRow("pkg:maven/com.google.cose/cose@20230908?repository_url=https://maven.google.com", "https://maven.google.com/com/google/cose/cose/20230908/")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUriPrefix)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<MavenArtifactType>> uris = await _projectManager.Object.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            if (purl?.Qualifiers?["repository_url"] == DEFAULT_MAVEN_ENDPOINT)
            {
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.Jar
                && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.jar")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.SourcesJar
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}-sources.jar")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.Pom
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.pom")));
            }
            else if (purlString.Contains("android.arch.core"))
            {
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.Pom
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.pom")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.Aar
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.aar")));
            }
            else if (purlString.Contains("com.google.cose"))
            {
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.Jar
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.jar")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.JavadocJar
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}-javadoc.jar")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenArtifactType.Pom
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.pom")));
            }
        }

        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6?repository_url=https://repo1.maven.org/maven2")] // Normal package
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha2?repository_url=https://maven.google.com")]
        [DataRow("pkg:maven/com.google.cose/cose@20230908?repository_url=https://maven.google.com")]
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
        [DataRow("pkg:maven/ant/ant@1.6", 12, "1.6.5")]
        [DataRow("pkg:maven/com.microsoft.fluentui/fluentui_listitem@0.0.8", 31, "0.3.4")]
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha3?repository_url=https://maven.google.com", 3, "1.0.0-alpha3")]
        [DataRow("pkg:maven/com.google.cose/cose@20230908?repository_url=https://maven.google.com", 1, "20230908")]
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
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha2?repository_url=https://maven.google.com")]
        [DataRow("pkg:maven/com.google.cose/cose@20230908?repository_url=https://maven.google.com")]
        public async Task PackageExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsTrue(await _projectManager.Object.PackageExistsAsync(purl, useCache: false));
        }

        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6")]
        [DataRow("pkg:maven/com.microsoft.fluentui/fluentui_listitem@0.0.8")]
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha2?repository_url=https://maven.google.com")]
        [DataRow("pkg:maven/com.google.cose/cose@20230908?repository_url=https://maven.google.com")]
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
            if (url.EndsWith(".pom"))
            {
                string lastModified = DateTime.Parse("9/8/2023 4:21:38 PM").ToString("R");

                httpMock
                    .When(HttpMethod.Get, url)
                    .Respond(req =>
                    {
                        var response = new HttpResponseMessage
                        {
                            StatusCode = statusCode,
                            Content = new StringContent(content, Encoding.UTF8, "application/json")
                        };
                        response.Content.Headers.Add("Last-Modified", lastModified);
                        return response;
                    });
            }
            else
            {
                httpMock
                    .When(HttpMethod.Get, url)
                    .Respond(statusCode, "application/json", content);
            }
        }
    }
}
