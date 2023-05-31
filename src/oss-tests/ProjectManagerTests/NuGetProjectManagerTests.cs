// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Contracts;
    using Helpers;
    using Model;
    using Model.Metadata;
    using Model.PackageExistence;
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
            { "https://api.nuget.org/v3/registration5-gz-semver2/slipeserver.scripting/index.json", Resources.slipeserver_scripting_json },
            { "https://api.nuget.org/v3/catalog0/data/2022.06.07.08.44.59/slipeserver.scripting.0.1.0-ci-20220607-083949.json", Resources.slipeserver_scripting_0_1_0_ci_20220607_083949_json },
        }.ToImmutableDictionary();
        
        private readonly IDictionary<string, string> _catalogPages = new Dictionary<string, string>()
        {
            { "https://api.nuget.org/v3/registration5-gz-semver2/slipeserver.scripting/page/0.1.0-ci-20220325-215611/0.1.0-ci-20220807-160739.json", Resources.slipeserver_scripting_catalogpage_2_json },
            { "https://api.nuget.org/v3/registration5-gz-semver2/slipeserver.scripting/page/0.1.0-ci-20221013-182634/0.1.0-ci-20221120-180516.json", Resources.slipeserver_scripting_catalogpage_3_json },
        }.ToImmutableDictionary();

        // Map PackageURLs to metadata as json.
        private readonly IDictionary<string, string> _metadata = new Dictionary<string, string>()
        {
            { "pkg:nuget/razorengine@4.2.3-beta1", Resources.razorengine_4_2_3_beta1_metadata_json },
            { "pkg:nuget/razorengine", Resources.razorengine_latest_metadata_json },
            { "pkg:nuget/slipeserver.scripting@0.1.0-CI-20220607-083949", Resources.slipeserver_scripting_0_1_0_ci_20220607_083949_json },
            { "pkg:nuget/slipeserver.scripting", Resources.slipeserver_scripting_0_1_0_ci_20220607_083949_json },
        }.ToImmutableDictionary();

        // Map PackageURLs to the list of versions as json.
        private readonly IDictionary<string, string> _versions = new Dictionary<string, string>()
        {
            { "pkg:nuget/razorengine@4.2.3-beta1", Resources.razorengine_versions_json },
            { "pkg:nuget/razorengine", Resources.razorengine_versions_json },
            { "pkg:nuget/slipeserver.scripting@0.1.0-CI-20220607-083949", Resources.slipeserver_scripting_versions_json },
            { "pkg:nuget/slipeserver.scripting", Resources.slipeserver_scripting_versions_json },
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
            
            foreach ((string url, string json) in _catalogPages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            mockHttp.When(HttpMethod.Get, "https://api.nuget.org/v3-flatcontainer/notarealpackage/0.0.0/notarealpackage.nuspec").Respond(HttpStatusCode.NotFound);
            mockHttp.When(HttpMethod.Get, "https://api.nuget.org/v3-flatcontainer/*.nupkg").Respond(HttpStatusCode.OK);
            mockHttp.When(HttpMethod.Get, "https://api.nuget.org/v3-flatcontainer/*.nuspec").Respond(HttpStatusCode.OK);

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;
            _projectManager = new NuGetProjectManager(".", new NuGetPackageActions(), _httpFactory);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1")]
        [DataRow("pkg:nuget/razorengine@4.2.3-Beta1")]
        [DataRow("pkg:nuget/rAzOrEnGiNe@4.2.3-Beta1")]
        [DataRow("pkg:nuget/SlipeServer.Scripting@0.1.0-CI-20220607-083949")]
        [DataRow("pkg:nuget/slipeserver.scripting@0.1.0-ci-20220607-083949")]
        public async Task TestNugetCaseInsensitiveHandlingPackageExistsSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);
            _projectManager = new NuGetProjectManager(".", null, _httpFactory);

            bool exists = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

            Assert.IsTrue(exists);
        }

        [TestMethod]
        public async Task TestNugetPackageWithVersionMetadataInPurlExists()
        {
            PackageURL purl = new("pkg:nuget/Pulumi@3.29.0-alpha.1649173720%2B667fd085");
            _projectManager = new NuGetProjectManager(".", null, _httpFactory);

            bool exists = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

            Assert.IsTrue(exists);
        }

        [TestMethod]
        public async Task TestNugetPackageWithNormalizedVersionInPurlExists()
        {
            PackageURL purl = new("pkg:nuget/Pulumi@3.29.0-alpha.1649173720");
            _projectManager = new NuGetProjectManager(".", null, _httpFactory);

            bool exists = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

            Assert.IsTrue(exists);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", false, "RazorEngine - A Templating Engine based on the Razor parser.", "Matthew Abbott, Ben Dornis, Matthias Dittrich")] // Normal package
        [DataRow("pkg:nuget/razorengine", false, "RazorEngine - A Templating Engine based on the Razor parser.", "Matthew Abbott, Ben Dornis, Matthias Dittrich", "4.5.1-alpha001")] // Normal package, no specified version
        [DataRow("pkg:nuget/slipeserver.scripting@0.1.0-CI-20220607-083949", false, "Scripting layer C# Server for MTA San Andreas", "Slipe", null)] // Normal package, no specified version, pre-release versions only, returns null for latest version by default
        [DataRow("pkg:nuget/slipeserver.scripting", true, "Scripting layer C# Server for MTA San Andreas", "Slipe", "0.1.0-ci-20221120-180516")] // Normal package, no specified version, pre-release versions only, returns latest pre-release for latest version because of override
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720%2B667fd085",true,"The Pulumi .NET SDK lets you write cloud programs in C#, F#, and VB.NET.","Pulumi")]
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720", true, "The Pulumi .NET SDK lets you write cloud programs in C#, F#, and VB.NET.","Pulumi")]
        public async Task MetadataSucceeds(string purlString, bool includePrerelease = false, string? description = null, string? authors = null, string? latestVersion = null)
        {
            PackageURL purl = new(purlString);
            NuGetPackageVersionMetadata? setupMetadata = null;
            IEnumerable<string>? setupVersions = null;

            if (_metadata.TryGetValue(purl.ToString(), out string? setupMetadataString))
            {
                setupMetadata = JsonConvert.DeserializeObject<NuGetPackageVersionMetadata>(setupMetadataString);
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
            _projectManager = string.IsNullOrWhiteSpace(purl.Version) ? new NuGetProjectManager(".", nugetPackageActions, _httpFactory) : _projectManager;

            PackageMetadata metadata = await _projectManager.GetPackageMetadataAsync(purl, includePrerelease: includePrerelease, useCache: false);

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
        [DataRow("pkg:nuget/slipeserver.scripting", 234, "0.1.0-ci-20221120-180516", true)]
        [DataRow("pkg:nuget/slipeserver.scripting", 0, null, false)]
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
                setupMetadata = JsonConvert.DeserializeObject<NuGetPackageVersionMetadata>(setupMetadataString);
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
            _projectManager = new NuGetProjectManager(".", nugetPackageActions, _httpFactory);

            List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, false, includePrerelease)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.FirstOrDefault());
        }
        
        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", true)]
        [DataRow("pkg:nuget/razorengine", true)]
        [DataRow("pkg:nuget/notarealpackage", false)]
        public async Task DetailedPackageExistsAsync_Succeeds(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions;

            if (exists)
            {
                // If we expect the package to exist, setup the helper as such.
                nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                    purl,
                    JsonConvert.DeserializeObject<NuGetPackageVersionMetadata>(_metadata[purl.ToString()]),
                    JsonConvert.DeserializeObject<IEnumerable<string>>(_versions[purl.ToString()])?.Reverse());
            }
            else
            {
                // If we expect the package to not exist, mock the actions to not do anything.
                nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions();
            }

            _projectManager = new NuGetProjectManager(".", nugetPackageActions, _httpFactory);

            IPackageExistence existence = await _projectManager.DetailedPackageExistsAsync(purl, useCache: false);

            Assert.AreEqual(exists, existence.Exists);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1")]
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720%2B667fd085")]
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720")]
        public async Task DetailedPackageVersionExistsAsync_ExistsSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            IPackageExistence existence = await _projectManager.DetailedPackageVersionExistsAsync(purl, useCache: false);

            Assert.AreEqual(new PackageVersionExists(), existence);
        }
        
        [TestMethod]
        public async Task DetailedPackageVersionExistsAsync_NotFoundSucceeds()
        {
            PackageURL purl = new("pkg:nuget/notarealpackage@0.0.0");

            IPackageExistence existence = await _projectManager.DetailedPackageVersionExistsAsync(purl, useCache: false);

            Assert.AreEqual(new PackageVersionNotFound(), existence);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", "2015-10-06T17:53:46.37+00:00")]
        [DataRow("pkg:nuget/razorengine@4.5.1-alpha001", "2017-09-02T05:17:55.973-04:00")]
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720%2B667fd085", "2022-04-05T16:56:44.043Z")]
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720", "2022-04-05T16:56:44.043Z")]
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
        [DataRow("pkg:nuget/nuget.server.core", true)]
        [DataRow("pkg:nuget/microsoft.cst.ossgadget.shared", true)]
        [DataRow("pkg:nuget/microsoft.office.interop.excel", false)]
        public async Task GetPackagePrefixReservedSucceeds(string purlString, bool expectedReserved)
        {
            PackageURL purl = new(purlString);
            bool isReserved = await _projectManager.GetHasReservedNamespaceAsync(purl, useCache: false);

            Assert.AreEqual(expectedReserved, isReserved);
        }
                
        [DataTestMethod]
        [DataRow("pkg:nuget/newtonsoft.json@13.0.1", 
            "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.1/newtonsoft.json.13.0.1.nupkg",
            "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.1/newtonsoft.json.nuspec")]
        [DataRow("pkg:nuget/razorengine@4.2.3-beta1", 
            "https://api.nuget.org/v3-flatcontainer/razorengine/4.2.3-beta1/razorengine.4.2.3-beta1.nupkg",
            "https://api.nuget.org/v3-flatcontainer/razorengine/4.2.3-beta1/razorengine.nuspec")]
        [DataRow("pkg:nuget/serilog@2.10.0", 
            "https://api.nuget.org/v3-flatcontainer/serilog/2.10.0/serilog.2.10.0.nupkg",
            "https://api.nuget.org/v3-flatcontainer/serilog/2.10.0/serilog.nuspec")]
        [DataRow("pkg:nuget/moq@4.17.2", 
            "https://api.nuget.org/v3-flatcontainer/moq/4.17.2/moq.4.17.2.nupkg",
            "https://api.nuget.org/v3-flatcontainer/moq/4.17.2/moq.nuspec")]
        [DataRow("pkg:nuget/moq@4.17.2?ignored_qualifier=ignored", 
            "https://api.nuget.org/v3-flatcontainer/moq/4.17.2/moq.4.17.2.nupkg",
            "https://api.nuget.org/v3-flatcontainer/moq/4.17.2/moq.nuspec")]
        [DataRow("pkg:nuget/SlipeServer.Scripting@0.1.0-CI-20220607-083949", 
            "https://api.nuget.org/v3-flatcontainer/slipeserver.scripting/0.1.0-ci-20220607-083949/slipeserver.scripting.0.1.0-ci-20220607-083949.nupkg",
            "https://api.nuget.org/v3-flatcontainer/slipeserver.scripting/0.1.0-ci-20220607-083949/slipeserver.scripting.nuspec")]
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720%2B667fd085",
           "https://api.nuget.org/v3-flatcontainer/pulumi/3.29.0-alpha.1649173720/pulumi.3.29.0-alpha.1649173720.nupkg",
            "https://api.nuget.org/v3-flatcontainer/pulumi/3.29.0-alpha.1649173720/pulumi.nuspec")]
        [DataRow("pkg:nuget/Pulumi@3.29.0-alpha.1649173720",
           "https://api.nuget.org/v3-flatcontainer/pulumi/3.29.0-alpha.1649173720/pulumi.3.29.0-alpha.1649173720.nupkg",
            "https://api.nuget.org/v3-flatcontainer/pulumi/3.29.0-alpha.1649173720/pulumi.nuspec")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedNuPkgUrl, string expectedNuSpecUri)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<NuGetProjectManager.NuGetArtifactType>> uris = _projectManager.GetArtifactDownloadUris(purl).ToList();

            var nupkgArtifactUri = uris
                .First(it => it.Type == NuGetProjectManager.NuGetArtifactType.Nupkg);

            Assert.AreEqual(expectedNuPkgUrl, nupkgArtifactUri.Uri.ToString());
            Assert.IsTrue(await _projectManager.UriExistsAsync(nupkgArtifactUri.Uri));

            var nuspecArtifactUrl = uris
                .First(it => it.Type == NuGetProjectManager.NuGetArtifactType.Nuspec);
            
            Assert.AreEqual(expectedNuSpecUri, nuspecArtifactUrl.Uri.ToString());
            Assert.IsTrue(await _projectManager.UriExistsAsync(nuspecArtifactUrl.Uri));

        }
        
        /// <summary>
        /// Until we implement proper support for custom service indexes (see https://docs.microsoft.com/en-us/nuget/api/service-index ),
        /// throw an exception instead of giving back bogus URLs when a package URL specifies a repository URL other than that of nuget.org
        /// </summary>
        [TestMethod]
        public void GetArtifactDownloadUris_NonPublicFeedURL_ThrowsNotImplementedException_Async()
        {
            PackageURL purl = new("pkg:nuget/moq@4.17.2?repository_url=https://test.com");
            
            Assert.ThrowsException<NotImplementedException>(() => _projectManager.GetArtifactDownloadUris(purl));
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
