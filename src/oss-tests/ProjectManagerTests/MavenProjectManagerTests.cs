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
            { "https://maven.google.com/web/index.html#android.arch.core:core:1.0.0-alpha3", Resources.maven_core_1_0_0_alpha3_html },
            { "https://maven.google.com/web/index.html#android.arch.core:core", Resources.maven_core_all_html },
            { "https://maven.google.com/web/index.html#androidx.appcompat:appcompat:1.7.0-rc01", Resources.maven_appcompat_1_7_0_rc01_html },
            { "https://maven.google.com/web/index.html#androidx.appcompat:appcompat", Resources.maven_appcompat_all_html },
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

            _projectManager = new Mock<MavenProjectManager>(".", new NoOpPackageActions(), _httpFactory, null) { CallBase = true };
        }

        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6?repository_url=https://repo1.maven.org/maven2", MavenSupportedUpstream.MavenCentralRepository, "https://repo1.maven.org/maven2/ant/ant/1.6/")]
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha3?repository_url=https://dl.google.com/android/maven2", MavenSupportedUpstream.GoogleMavenRepository, "https://dl.google.com/android/maven2/android/arch/core/core/1.0.0-alpha3/")]
        [DataRow("pkg:maven/androidx.appcompat/appcompat@1.7.0-rc01?repository_url=https://dl.google.com/android/maven2", MavenSupportedUpstream.GoogleMavenRepository, "https://dl.google.com/android/maven2/androidx/appcompat/appcompat/1.7.0-rc01/")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, MavenSupportedUpstream upstream, string expectedUriPrefix)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<MavenProjectManager.MavenArtifactType>> uris = await _projectManager.Object.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            if (upstream == MavenSupportedUpstream.MavenCentralRepository)
            {
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.Jar
                && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.jar")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.SourcesJar
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}-sources.jar")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.Pom
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.pom")));
            }
            else if (upstream  == MavenSupportedUpstream.GoogleMavenRepository)
            {
                
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.Aar
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.aar")));
                Assert.IsNotNull(uris.SingleOrDefault(artifact => artifact.Type == MavenProjectManager.MavenArtifactType.Pom
                    && artifact.Uri == new System.Uri(expectedUriPrefix + $"{purl.Name}-{purl.Version}.pom")));
            }
        }

        [DataTestMethod]
        [DataRow("pkg:maven/ant/ant@1.6?repository_url=https://repo1.maven.org/maven2")] // Normal package
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha3?repository_url=https://dl.google.com/android/maven2")]
        [DataRow("pkg:maven/androidx.appcompat/appcompat@1.7.0-rc01?repository_url=https://dl.google.com/android/maven2")]
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
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha3?repository_url=https://dl.google.com/android/maven2", 3, "1.0.0-alpha3")]
        [DataRow("pkg:maven/androidx.appcompat/appcompat@1.7.0-rc01?repository_url=https://dl.google.com/android/maven2", 56, "1.7.0")]
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
        [DataRow("pkg:maven/android.arch.core/core@1.0.0-alpha3?repository_url=https://dl.google.com/android/maven2")]
        [DataRow("pkg:maven/androidx.appcompat/appcompat@1.7.0-rc01?repository_url=https://dl.google.com/android/maven2")]
        public async Task PackageVersionExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsTrue(await _projectManager.Object.PackageVersionExistsAsync(purl, useCache: false));
        }

        [DataTestMethod]
        [DataRow("https://maven.google.com/web/index.html#android.arch.core:core:1.0.0-alpha3")]
        [DataRow("https://maven.google.com/web/index.html#androidx.appcompat:appcompat:1.7.0-rc01")]
        public async Task GoogleMavenRepositoryMetadataParserSucceeds(string url)
        {
            var metadata = await _projectManager.Object.GoogleMavenRepositoryMetadataParserHelperAsync(url);

            if (url.Contains("android.arch.core"))
            {
                var expectedResult = new Dictionary<string, string>
                {
                    { "Project", "https://developer.android.com/topic/libraries/architecture/index.html" },
                    { "Artifact(s)", "aar, pom" },
                    { "Developer(s)", "The Android Open Source Project" },
                    { "License(s)", "The Apache Software License Version 2.0" },
                    { "Group ID", "android.arch.core" },
                    { "Artifact ID", "core" },
                    { "Version", "1.0.0-alpha3" },
                    { "Gradle Groovy DSL", "" },
                    { "Gradle Kotlin DSL", "" },
                    { "Last Updated Date", "9/20/2019" }
                };
                CollectionAssert.AreEquivalent(metadata, expectedResult);
            }
            else if (url.Contains("androidx.appcompat"))
            {
                var expectedResult = new Dictionary<string, string>
                {
                    { "Name", "AppCompat" },
                    { "Description", "Provides backwards-compatible implementations of UI-related Android SDK functionality including dark mode and Material theming." },
                    { "Project", "https://developer.android.com/jetpack/androidx/releases/appcompat#1.7.0-rc01" },
                    { "Artifact(s)", "source, gpg (source), appcompat-1.7.0-rc01-versionMetadata.json, gpg (appcompat-1.7.0-rc01-versionMetadata.json), aar, gpg (aar), gradle-module-metadata, gpg (gradle-module-metadata), pom, gpg (appcompat-1.7.0-rc01.pom)" },
                    { "Developer(s)", "The Android Open Source Project" },
                    { "License(s)", "The Apache Software License Version 2.0" },
                    { "Group ID", "androidx.appcompat" },
                    { "Artifact ID", "appcompat" },
                    { "Version", "1.7.0-rc01" },
                    { "Gradle Groovy DSL", "" },
                    { "Gradle Kotlin DSL", "" },
                    { "Last Updated Date", "5/14/2024" },
                    { "Organization", "The Android Open Source Project" }
                };
                CollectionAssert.AreEquivalent(metadata, expectedResult);
            }
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
