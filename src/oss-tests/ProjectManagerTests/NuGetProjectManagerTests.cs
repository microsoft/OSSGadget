// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Contracts;
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
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NuGetProjectManagerTests
    {
        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://api.nuget.org/v3/registration5-gz-semver2/razorengine/index.json", Resources.razorengine_json },
            { "https://api.nuget.org/v3/catalog0/data/2022.03.11.23.17.27/razorengine.4.2.3-beta1.json", Resources.razorengine_4_2_3_beta1_json },
        }.ToImmutableDictionary();
        
        // Map PackageURLs to metadata as json.
        private readonly IDictionary<string, string> _metadata = new Dictionary<string, string>()
        {
            { "pkg:nuget/razorengine@4.2.3-beta1", Resources.razorengine_4_2_3_beta1_metadata_json },
            { "pkg:nuget/razorengine", Resources.razorengine_latest_metadata_json },
        }.ToImmutableDictionary();
        
        // Map PackageURLs to the list of versions as json.
        private readonly IDictionary<string, string> _versions = new Dictionary<string, string>()
        {
            { "pkg:nuget/razorengine@4.2.3-beta1", Resources.razorengine_versions_json },
            { "pkg:nuget/razorengine", Resources.razorengine_versions_json },
        }.ToImmutableDictionary();

        private NuGetProjectManager _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        public NuGetProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();

            MockHttpMessageHandler mockHttp = new();

            // Mock getting the registration endpoint.
            mockHttp
                .When(HttpMethod.Get, "https://api.nuget.org/v3/index.json")
                .Respond(HttpStatusCode.OK, "application/json", Resources.nuget_registration_json);

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            mockHttp.When(HttpMethod.Get, "https://api.nuget.org/v3-flatcontainer/*.nupkg").Respond(HttpStatusCode.OK);
            mockHttp.When(HttpMethod.Get, "https://api.nuget.org/v3-flatcontainer/*.nuspec").Respond(HttpStatusCode.OK);
 
            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;
            _projectManager = new NuGetProjectManager(".", new NuGetPackageActions(), _httpFactory);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", "RazorEngine - A Templating Engine based on the Razor parser.", "Matthew Abbott, Ben Dornis, Matthias Dittrich")] // Normal package
        [DataRow("pkg:nuget/razorengine", "RazorEngine - A Templating Engine based on the Razor parser.", "Matthew Abbott, Ben Dornis, Matthias Dittrich", "4.5.1-alpha001")] // Normal package, no specified version
        public async Task MetadataSucceeds(string purlString, string? description = null, string? authors = null, string? latestVersion = null)
        {
            PackageURL purl = new(purlString);
            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                JsonConvert.DeserializeObject<NuGetPackageVersionMetadata>(_metadata[purl.ToString()]),
                JsonConvert.DeserializeObject<IEnumerable<string>>(_versions[purl.ToString()])?.Reverse());
            _projectManager = new NuGetProjectManager(".", nugetPackageActions, _httpFactory);

            PackageMetadata metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

            Assert.AreEqual(purl.Name, metadata.Name, ignoreCase: true);
            
            // If a version was specified, assert the response is for this version, otherwise assert for the latest version.
            Assert.AreEqual(!string.IsNullOrWhiteSpace(purl.Version) ? purl.Version : latestVersion,
                metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
            if (!string.IsNullOrWhiteSpace(authors))
            {
                List<User> authorsList = authors.Split(", ").Select(author => new User() { Name = author }).ToList();
                CollectionAssert.AreEquivalent(authorsList, metadata.Authors);
            }
        }
        
        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", 84, "4.5.1-alpha001")]
        [DataRow("pkg:nuget/razorengine", 84, "4.5.1-alpha001")]
        [DataRow("pkg:nuget/razorengine", 40, "3.10.0", false)]
        public async Task EnumerateVersionsSucceeds(
            string purlString, 
            int count, 
            string latestVersion, 
            bool includePrerelease = true)
        {
            PackageURL purl = new(purlString);
            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                JsonConvert.DeserializeObject<NuGetPackageVersionMetadata>(_metadata[purl.ToString()]),
                JsonConvert.DeserializeObject<IEnumerable<string>>(_versions[purl.ToString()])?.Reverse(),
                includePrerelease: includePrerelease);
            _projectManager = new NuGetProjectManager(".", nugetPackageActions, _httpFactory);

            List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, false, includePrerelease)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", "2015-10-06T17:53:46.37+00:00")]
        [DataRow("pkg:nuget/razorengine@4.5.1-alpha001", "2017-09-02T05:17:55.973-04:00")]
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
        [DataRow("pkg:nuget/newtonsoft.json@13.0.1", "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.1/newtonsoft.json")]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", "https://api.nuget.org/v3-flatcontainer/razorengine/4.2.3-beta1/razorengine")]
        [DataRow("pkg:nuget/serilog@2.10.0", "https://api.nuget.org/v3-flatcontainer/serilog/2.10.0/serilog")]
        [DataRow("pkg:nuget/moq@4.17.2", "https://api.nuget.org/v3-flatcontainer/moq/4.17.2/moq")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUriBase)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<NuGetProjectManager.NuGetArtifactType>> uris = _projectManager.GetArtifactDownloadUris(purl).ToList();

            string expectedNuPkgUri = $"{expectedUriBase}.{purl.Version}.nupkg";
            Assert.AreEqual(expectedNuPkgUri, uris.First().Uri.AbsoluteUri);
            Assert.AreEqual(".nupkg", uris.First().Extension);
            Assert.AreEqual(NuGetProjectManager.NuGetArtifactType.Nupkg, uris.First().Type);
            Assert.IsTrue(await _projectManager.UriExistsAsync(uris.First().Uri));
            
            string expectedNuspecUri = $"{expectedUriBase}.nuspec";
            Assert.AreEqual(expectedNuspecUri, uris.Last().Uri.AbsoluteUri);
            Assert.AreEqual(".nuspec", uris.Last().Extension);
            Assert.AreEqual(NuGetProjectManager.NuGetArtifactType.Nuspec, uris.Last().Type);
            Assert.IsTrue(await _projectManager.UriExistsAsync(uris.Last().Uri));

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
