// Copyright (c) Microsoft Corporation. Licensed under the MIT License.


namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Model;
    using Moq;
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

        private readonly NuGetProjectManager _projectManager;
        
        public NuGetProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            
            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }
 
            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            
            _projectManager = new NuGetProjectManager(mockFactory.Object, ".");
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", "RazorEngine - A Templating Engine based on the Razor parser.", "Matthew Abbott, Ben Dornis, Matthias Dittrich")] // Normal package
        public async Task MetadataSucceeds(string purlString, string? description = null, string? authors = null)
        {
            PackageURL purl = new(purlString);
            PackageMetadata metadata = await _projectManager.GetPackageMetadata(purl, useCache: false);

            Assert.AreEqual(purl.Name, metadata.Name, ignoreCase: true);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
            if (!string.IsNullOrEmpty(authors))
            {
                List<User> authorsList = authors.Split(", ").Select(author => new User() { Name = author }).ToList();
                CollectionAssert.AreEquivalent(authorsList, metadata.Authors);
            }
        }
        
        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine", 84, "4.5.1-alpha001")]
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
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
