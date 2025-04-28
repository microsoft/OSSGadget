namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Contracts;
    using FluentAssertions;
    using Helpers;
    using Model;
    using Model.Metadata;
    using Moq;
    using Newtonsoft.Json;
    using oss;
    using PackageActions;
    using PackageManagers;
    using PackageUrl;
    using RichardSzalay.MockHttp;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NuGetProjectManagerV2Tests
    {
        private NuGetV2ProjectManager _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://www.powershellgallery.com/api/v2/FindPackagesById()?id='psreadline'&semVerLevel=2.0.0", Resources.psreadline_xml },
        }.ToImmutableDictionary();

        // Map PackageURLs to metadata as json.
        private readonly IDictionary<string, string> _metadata = new Dictionary<string, string>()
        {
            { "pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_v2_4_1_beta_xml },
            { "pkg:nuget/PSReadLine?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_v2_4_1_beta_xml },
        }.ToImmutableDictionary();

        // Map PackageURLs to the list of versions as json.
        private readonly IDictionary<string, string> _versions = new Dictionary<string, string>()
        {
            { "pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_versions_json },
            { "pkg:nuget/PSReadLine?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_versions_json },
        }.ToImmutableDictionary();

        public NuGetProjectManagerV2Tests()
        {
            Mock<IHttpClientFactory> mockFactory = new();

            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string xml) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, xml, mockHttp);
            }

            mockHttp.When(HttpMethod.Get, "https://www.powershellgallery.com/api/v2/package/*").Respond(HttpStatusCode.OK);

            // Set fallback to throw an exception for unmatched requests
            mockHttp.Fallback.Throw(new InvalidOperationException("Unmatched HTTP request. Ensure all requests are properly mocked."));

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;
            _projectManager = new NuGetV2ProjectManager(".", NuGetPackageActions.CreateV2(), _httpFactory);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/")]
        [DataRow("pkg:nuget/psreadline@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/")]
        [DataRow("pkg:nuget/Az.Accounts@4.0.0?repository_url=https://www.powershellgallery.com/api/v2/")]
        public async Task TestNugetCaseInsensitiveHandlingPackageExistsSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            bool exists = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

            Assert.IsTrue(exists);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [DataRow("pkg:nuget/NonExistentPackageXYZ@0.0.0?repository_url=https://www.powershellgallery.com/api/v2/", false)]
        public async Task PackageExistsSucceeds(string purlString, bool expected)
        {
            PackageURL purl = new(purlString);

            bool exists = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

            exists.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/Az.Accounts@4.0.0?repository_url=https://www.powershellgallery.com/api/v2/", "https://www.powershellgallery.com/api/v2/package/az.accounts/4.0.0")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedNuPkgUrl)
        {
            PackageURL purl = new(purlString);
            var uris = _projectManager.GetArtifactDownloadUrisAsync(purl);

            var nupkgArtifactUri = await uris
                .FirstAsync(it => it.Type == BaseNuGetProjectManager.NuGetArtifactType.Nupkg);

            Assert.AreEqual(expectedNuPkgUrl, nupkgArtifactUri.Uri.ToString());
            Assert.IsTrue(await _projectManager.UriExistsAsync(nupkgArtifactUri.Uri));
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", false, "Great command line editing in the PowerShell console host")]
        [DataRow("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", true, "Great command line editing in the PowerShell console host", "2.4.1-beta1")]
        public async Task MetadataSucceeds(string purlString, bool includePrerelease = false, string? description = null, string? latestVersion = null)
        {
            PackageURL purl = new(purlString);


            NuGetPackageVersionMetadata? setupMetadata = null;
            IEnumerable<string>? setupVersions = null;

            if (_metadata.TryGetValue(purl.ToString(), out string? setupMetadataString))
            {
                NuGetV2PackageVersionMetadata nuGetV2PackageVersionMetadata = DeserializeFromXml<NuGetV2PackageVersionMetadata>(setupMetadataString);
                setupMetadata = new NuGetPackageVersionMetadata(nuGetV2PackageVersionMetadata);
            }

            if (_versions.TryGetValue(purl.ToString(), out string? setupVersionsString))
            {
                setupVersions = JsonConvert.DeserializeObject<IEnumerable<string>>(setupVersionsString);
            }

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                setupMetadata,
                setupVersions?.Reverse());

            // Use mocked response if version is not provided.
            _projectManager = string.IsNullOrWhiteSpace(purl.Version) ? new NuGetV2ProjectManager(".", nugetPackageActions, _httpFactory) : _projectManager;

            PackageMetadata metadata = await _projectManager.GetPackageMetadataAsync(purl, includePrerelease: includePrerelease, useCache: false);

            Assert.AreEqual(purl.Name, metadata.Name, ignoreCase: true);

            // If a version was specified, assert the response is for this version, otherwise assert for the latest version.
            Assert.AreEqual(!string.IsNullOrWhiteSpace(purl.Version) ? purl.Version : latestVersion,
                metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", 45, "2.4.1-beta1")]
        [DataRow("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", 45, "2.4.1-beta1")]
        public async Task EnumerateVersionsSucceeds(
            string purlString,
            int count,
            string? latestVersion,
            bool includePrerelease = true)
        {
            PackageURL purl = new(purlString);
            NuGetPackageVersionMetadata? setupMetadata = null;
            IEnumerable<string>? setupVersions = null;

            if (_metadata.TryGetValue(purl.ToString(), out string? setupMetadataString))
            {
                NuGetV2PackageVersionMetadata nuGetV2PackageVersionMetadata = DeserializeFromXml<NuGetV2PackageVersionMetadata>(setupMetadataString);
                setupMetadata = new NuGetPackageVersionMetadata(nuGetV2PackageVersionMetadata);
            }

            if (_versions.TryGetValue(purl.ToString(), out string? setupVersionsString))
            {
                setupVersions = JsonConvert.DeserializeObject<IEnumerable<string>>(setupVersionsString);
            }

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                setupMetadata,
                setupVersions?.Reverse(),
                includePrerelease: includePrerelease);
            _projectManager = new NuGetV2ProjectManager(".", nugetPackageActions, _httpFactory);

            List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, false, includePrerelease)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.FirstOrDefault());
        }


        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine@2.0.0?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [DataRow("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", false)] // no version provided
        [DataRow("pkg:nuget/Az.Accounts@2.5.3?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [DataRow("pkg:nuget/PowerShellGet@2.2.5?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [DataRow("pkg:nuget/notarealpackage@0.0.0?repository_url=https://www.powershellgallery.com/api/v2/", false)] // not a real package
        public async Task DetailedPackageVersionExistsAsync_ExistsSucceeds(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);

            IPackageExistence existence = await _projectManager.DetailedPackageVersionExistsAsync(purl, useCache: false);

            Assert.AreEqual(exists, existence.Exists);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine@2.0.0?repository_url=https://www.powershellgallery.com/api/v2/", "2020-02-12T02:22:59.793+00:00")]
        [DataRow("pkg:nuget/Az.Accounts@2.5.3?repository_url=https://www.powershellgallery.com/api/v2/", "2021-09-07T12:57:05.487+00:00")]
        public async Task GetPublishedAtSucceeds(string purlString, string? expectedTime = null)
        {
            PackageURL purl = new(purlString);
            DateTime? time = await _projectManager.GetPublishedAtAsync(purl, useCache: false);

            if (expectedTime == null)
            {
                Assert.IsNull(time);
            }
            else
            {
                Assert.AreEqual(DateTime.Parse(expectedTime), time);
            }
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/")]
        [DataRow("pkg:nuget/Az.Accounts?repository_url=https://www.powershellgallery.com/api/v2/")]
        [DataRow("pkg:nuget/Microsoft.Graph.Authentication?repository_url=https://www.powershellgallery.com/api/v2/")]
        public async Task GetPackagePrefixReserved_ReturnsFalse(string purlString)
        {
            PackageURL purl = new(purlString);
            bool isReserved = await _projectManager.GetHasReservedNamespaceAsync(purl, useCache: false);

            Assert.IsFalse(isReserved); // Reserved namespaces are not supported in NuGet V2
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [DataRow("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [DataRow("pkg:nuget/notarealpackage?repository_url=https://www.powershellgallery.com/api/v2/", false)]
        public async Task DetailedPackageExistsAsync_Succeeds(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions;

            if (exists)
            {
                var setupMetadataString = _metadata[purl.ToString()];
                NuGetV2PackageVersionMetadata nuGetV2PackageVersionMetadata = DeserializeFromXml<NuGetV2PackageVersionMetadata>(setupMetadataString);
                var setupMetadata = new NuGetPackageVersionMetadata(nuGetV2PackageVersionMetadata);
                // If we expect the package to exist, setup the helper as such.
                nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                    purl,
                    setupMetadata,
                    JsonConvert.DeserializeObject<IEnumerable<string>>(_versions[purl.ToString()])?.Reverse());
            }
            else
            {
                // If we expect the package to not exist, mock the actions to not do anything.
                nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions();
            }

            _projectManager = new NuGetV2ProjectManager(".", nugetPackageActions, _httpFactory);

            IPackageExistence existence = await _projectManager.DetailedPackageExistsAsync(purl, useCache: false);

            Assert.AreEqual(exists, existence.Exists);
        }

        private static void MockHttpFetchResponse(
            HttpStatusCode statusCode,
            string url,
            string content,
            MockHttpMessageHandler httpMock)
        {
            httpMock
                .When(HttpMethod.Get, url)
                .Respond(statusCode, "application/xml", content);
        }

        public static T DeserializeFromXml<T>(string xml)
        {
            XmlSerializer serializer = new(typeof(T));
            using StringReader reader = new(xml);
            return (T)serializer.Deserialize(reader);
        }
    }

}
