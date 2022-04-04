// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Contracts;
    using Helpers;
    using Model;
    using Model.Metadata;
    using Model.PackageActions;
    using Moq;
    using Newtonsoft.Json;
    using oss;
    using PackageManagers;
    using PackageUrl;
    using RichardSzalay.MockHttp;
    using System.Collections.Generic;
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
        };
        
        // Map PackageURLs to metadata as json.
        private readonly IDictionary<string, string> _metadata = new Dictionary<string, string>()
        {
            { "pkg:nuget/razorengine@4.2.3-beta1", Resources.razorengine_4_2_3_beta1_metadata_json },
            { "pkg:nuget/razorengine", Resources.razorengine_latest_metadata_json },
        };
        
        // Map PackageURLs to the list of versions as json.
        private readonly IDictionary<string, string> _versions = new Dictionary<string, string>()
        {
            { "pkg:nuget/razorengine@4.2.3-beta1", Resources.razorengine_versions_json },
            { "pkg:nuget/razorengine", Resources.razorengine_versions_json },
        };

        private NuGetProjectManager? _projectManager;
        private readonly Mock<IHttpClientFactory> _mockFactory = new();

        public NuGetProjectManagerTests()
        {
            MockHttpMessageHandler mockHttp = new();

            // Mock getting the registration endpoint.
            mockHttp
                .When(HttpMethod.Get, "https://api.nuget.org/v3/index.json")
                .Respond(HttpStatusCode.OK, "application/json", Resources.nuget_registration_json);

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }
 
            _mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", "RazorEngine - A Templating Engine based on the Razor parser.", "Matthew Abbott, Ben Dornis, Matthias Dittrich")] // Normal package
        [DataRow("pkg:nuget/razorengine", "RazorEngine - A Templating Engine based on the Razor parser.", "Matthew Abbott, Ben Dornis, Matthias Dittrich", "4.5.1-alpha001")] // Normal package, no specified version
        public async Task MetadataSucceeds(string purlString, string? description = null, string? authors = null, string? latestVersion = null)
        {
            PackageURL purl = new(purlString);
            NuGetPackageActions? nugetPackageActions = PackageActionsHelper<NuGetPackageActions, NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                JsonConvert.DeserializeObject<NuGetPackageVersionMetadata>(_metadata[purl.ToString()]),
                JsonConvert.DeserializeObject<IEnumerable<string>>(_versions[purl.ToString()]));
            _projectManager = new NuGetProjectManager(".", nugetPackageActions, _mockFactory.Object);

            PackageMetadata metadata = await _projectManager.GetPackageMetadata(purl, useCache: false);

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
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
            NuGetPackageActions? nugetPackageActions = PackageActionsHelper<NuGetPackageActions, NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                JsonConvert.DeserializeObject<NuGetPackageVersionMetadata>(_metadata[purl.ToString()]),
                JsonConvert.DeserializeObject<IEnumerable<string>>(_versions[purl.ToString()]));
            _projectManager = new NuGetProjectManager(".", nugetPackageActions, _mockFactory.Object);

            List<string> versions = (await _projectManager.EnumerateVersions(purl, useCache: false)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
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