// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Contracts;
    using Extensions;
    using FluentAssertions;
    using Model;
    using Model.Enums;
    using Model.PackageExistence;
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
    using System.Text.Json;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NPMProjectManagerTests
    {
        private static readonly IDictionary<string, (IPackageExistence packageVersionExistence, bool versionPulled, bool packagePulled, bool consideredMalicious)> _packageVersionExistence = new Dictionary<string, (IPackageExistence packageVersionExistence, bool versionPulled, bool packagePulled, bool consideredMalicious)>()
        {
            { "currently_exists",
                (new PackageVersionExists(),
                    versionPulled: false,
                    packagePulled: false,
                    consideredMalicious: false)
            },
            { "version_pulled",
                (new PackageVersionRemoved(new HashSet<PackageVersionRemovalReason>(new[] { PackageVersionRemovalReason.VersionUnpublished })),
                    versionPulled: true,
                    packagePulled: false,
                    consideredMalicious: false)
            },
            { "version_never_existed",
                (new PackageVersionNotFound(),
                    versionPulled: false,
                    packagePulled: false,
                    consideredMalicious: false)
            },
            { "package_considered_malicious",
                (new PackageVersionRemoved(new HashSet<PackageVersionRemovalReason>(new[] { PackageVersionRemovalReason.RemovedByRepository })),
                    versionPulled: false,
                    packagePulled: false,
                    consideredMalicious: true)
            }
        };
        
        private static readonly IDictionary<string, (IPackageExistence packageExistence, bool pulled, bool consideredMalicious)> _packageExistence = new Dictionary<string, (IPackageExistence packageExistence, bool pulled, bool consideredMalicious)>()
        {
            { "currently_exists",
                (new PackageExists(),
                    pulled: false,
                    consideredMalicious: false)
            },
            { "never_existed",
                (new PackageNotFound(),
                    pulled: false,
                    consideredMalicious: false)
            },
            { "pulled",
                (new PackageRemoved(new HashSet<PackageRemovalReason>(new[] { PackageRemovalReason.PackageUnpublished })),
                    pulled: true,
                    consideredMalicious: false)
            },
            { "considered_malicious",
                (new PackageRemoved(new HashSet<PackageRemovalReason>(new[] { PackageRemovalReason.RemovedByRepository })),
                    pulled: false,
                    consideredMalicious: true)
            },
        };

        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://registry.npmjs.org/lodash", Resources.lodash_json },
            { "https://registry.npmjs.org/lodash.js", Resources.lodashjs_json },
            { "https://registry.npmjs.org/%40somosme/webflowutils", Resources.unpublishedpackage_json },
            { "https://registry.npmjs.org/%40angular/core", Resources.angular_core_json },
            { "https://registry.npmjs.org/%40achievementify/client", Resources.achievementify_client_json },
            { "https://registry.npmjs.org/ds-modal", Resources.ds_modal_json },
            { "https://registry.npmjs.org/monorepolint", Resources.monorepolint_json },
            { "https://registry.npmjs.org/rly-cli", Resources.rly_cli_json },
            { "https://registry.npmjs.org/tslib", Resources.tslib_json },
            { "https://registry.npmjs.org/example", Resources.minimum_json_json },
        }.ToImmutableDictionary();

        private readonly IDictionary<string, string> _packageVersions = new Dictionary<string, string>()
        {
            { "https://registry.npmjs.org/lodash/4.17.15", "mockContent" },
            { "https://registry.npmjs.org/@angular/core/13.2.5","mockContent" },
            { "https://registry.npmjs.org/ds-modal/0.0.2", "mockContent" },
            { "https://registry.npmjs.org/monorepolint/0.4.0", "mockContent" },
            { "https://registry.npmjs.org/example/0.0.0", "mockContent" },
            { "https://registry.npmjs.org/rly-cli/0.0.2", "mockContent" },
            { "https://registry.npmjs.org/lodash.js/0.0.1-security", "mockContent" },
            { "https://registry.npmjs.org/tslib/2.4.1", "mockContent" },
        }.ToImmutableDictionary();

        private readonly Mock<NPMProjectManager> _projectManager;
        private readonly IHttpClientFactory _httpFactory;
        
        public NPMProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            
            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            foreach ((string url, string content) in _packageVersions)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, content, mockHttp);
            }

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new Mock<NPMProjectManager>(".", new NoOpPackageActions(), _httpFactory) { CallBase = true };
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "Lodash modular utilities.")] // Normal package
        [DataRow("pkg:npm/%40angular/core@13.2.5", "Angular - the core framework")] // Scoped package
        [DataRow("pkg:npm/ds-modal@0.0.2", "")] // No Description at package level, and empty string description on version level
        [DataRow("pkg:npm/monorepolint@0.4.0")] // No Author property, and No Description
        [DataRow("pkg:npm/example@0.0.0")] // Pretty much only name, and version
        [DataRow("pkg:npm/rly-cli@0.0.2", "RLY CLI allows you to setup fungilble SPL tokens and call Rally token programs from the command line.")] // Author property is an empty string
        public async Task MetadataSucceeds(string purlString, string? description = null)
        {
            PackageURL purl = new(purlString);
            PackageMetadata? metadata = await _projectManager.Object.GetPackageMetadataAsync(purl, useCache: false);

            Assert.IsNotNull(metadata);
            Assert.AreEqual(purl.GetFullName(), metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", 114, "4.17.21")]
        [DataRow("pkg:npm/%40angular/core@13.2.5", 566, "13.2.6")]
        [DataRow("pkg:npm/ds-modal@0.0.2", 3, "0.0.2")]
        [DataRow("pkg:npm/monorepolint@0.4.0", 88, "0.4.0")]
        [DataRow("pkg:npm/example@0.0.0", 1, "0.0.0")]
        [DataRow("pkg:npm/rly-cli@0.0.2", 4, "0.0.4")]
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
            List<string> versions = (await _projectManager.Object.EnumerateVersionsAsync(purl, useCache: false)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15")]
        [DataRow("pkg:npm/%40angular/core@13.2.5")]
        [DataRow("pkg:npm/ds-modal@0.0.2")]
        [DataRow("pkg:npm/monorepolint@0.4.0")]
        [DataRow("pkg:npm/example@0.0.0")]
        [DataRow("pkg:npm/rly-cli@0.0.2")]
        [DataRow("pkg:npm/lodash.js@0.0.1-security")]
        [DataRow("pkg:npm/tslib@2.4.1")]
        public async Task PackageVersionExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsTrue(await _projectManager.Object.PackageVersionExistsAsync(purl, useCache: false));
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@1.2.3.4.5.6.7")]
        [DataRow("pkg:npm/%40angular/core@1.2.3.4.5.6.7")]
        [DataRow("pkg:npm/%40achievementify/client@0.2.1")]
        public async Task PackageVersionDoesntExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsFalse(await _projectManager.Object.PackageVersionExistsAsync(purl, useCache: false));
        }
        
        [DataTestMethod]
        [DataRow("pkg:npm/tslib@2.4.1", "currently_exists")]
        public async Task DetailedPackageVersionExistsAsync_PurlSucceeds(string purlString, string existenceKey)
        {
            PackageURL purl = new(purlString);

            var existence = await _projectManager.Object.DetailedPackageVersionExistsAsync(purl, useCache: false);
            existence.Should().BeEquivalentTo(_packageVersionExistence[existenceKey].packageVersionExistence);
        }

        [DataTestMethod]
        [DataRow("currently_exists")] // The package version currently exists
        [DataRow("version_pulled")] // The package version was pulled from the registry
        [DataRow("version_never_existed")] // The package version never existed
        [DataRow("package_considered_malicious")] // The package was removed for security
        public async Task DetailedPackageVersionExistsAsyncSucceeds(
            string key)
        {
            IPackageExistence expectedPackageVersionExistence = _packageVersionExistence[key].packageVersionExistence;
            bool versionPulled = _packageVersionExistence[key].versionPulled;
            bool consideredMalicious = _packageVersionExistence[key].consideredMalicious;
            
            if (!expectedPackageVersionExistence.Exists)
            {
                _projectManager
                    .Setup(p => 
                        p.GetMetadataAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                    .ReturnsAsync(null as string);
            }

            _projectManager
                .Setup(p => 
                    p.PackageVersionExistsAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(expectedPackageVersionExistence.Exists);
            _projectManager
                .Setup(p => 
                    p.PackageVersionPulled(It.IsAny<PackageURL>(), It.IsAny<JsonElement>()))
                .Returns(versionPulled);

            _projectManager
                .Setup(p => 
                    p.PackageConsideredMalicious(It.IsAny<JsonElement>()))
                .Returns(consideredMalicious);

            IPackageExistence packageVersionExistenceResponse =
                await _projectManager.Object.DetailedPackageVersionExistsAsync(new PackageURL("pkg:npm/example@0.0.0"),
                    useCache: false);

            packageVersionExistenceResponse.Should().BeEquivalentTo(expectedPackageVersionExistence);
        }
        
        [DataTestMethod]
        [DataRow("currently_exists")] // The package currently exists
        [DataRow("never_existed")] // The package never existed
        [DataRow("pulled")] // The package existed but was removed
        [DataRow("considered_malicious")] // The package was removed for security reasons
        public async Task DetailedPackageExistsAsyncSucceeds(
            string key)
        {
            IPackageExistence expectedPackageExistence = _packageExistence[key].packageExistence;
            bool pulled = _packageExistence[key].pulled;
            bool consideredMalicious = _packageExistence[key].consideredMalicious;

            if (!expectedPackageExistence.Exists)
            {
                _projectManager
                    .Setup(p => 
                        p.GetMetadataAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                    .ReturnsAsync(null as string);
            }

            _projectManager
                .Setup(p => 
                    p.PackageExistsAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(expectedPackageExistence.Exists);
            _projectManager
                .Setup(p => 
                    p.PackagePulled(It.IsAny<JsonElement>()))
                .Returns(pulled);
            _projectManager
                .Setup(p => 
                    p.PackageConsideredMalicious(It.IsAny<JsonElement>()))
                .Returns(consideredMalicious);

            IPackageExistence packageExistenceResponse =
                await _projectManager.Object.DetailedPackageExistsAsync(new PackageURL("pkg:npm/example@0.0.0"),
                    useCache: false);

            packageExistenceResponse.Should().BeEquivalentTo(expectedPackageExistence);
        }
        
        [DataTestMethod]
        [DataRow("pkg:npm/%40somosme/webflowutils@1.0.0")]
        [DataRow("pkg:npm/%40somosme/webflowutils@1.2.3", false)]
        [DataRow("pkg:npm/%40achievementify/client@0.2.1")]
        [DataRow("pkg:npm/%40achievementify/client@0.2.3", false)]
        public async Task PackageVersionPulledAsync(string purlString, bool expectedPulled = true)
        {
            PackageURL purl = new(purlString);
            
            string? content = await _projectManager.Object.GetMetadataAsync(purl);

            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            Assert.AreEqual(expectedPulled,  _projectManager.Object.PackageVersionPulled(purl, root));
        }
        
        [DataTestMethod]
        [DataRow("pkg:npm/lodash.js")]
        [DataRow("pkg:npm/lodash.js@1.0.0")]
        [DataRow("pkg:npm/lodash", false)]
        public async Task PackageSecurityHoldingAsync(string purlString, bool expectedToHaveSecurityHolding = true)
        {
            PackageURL purl = new(purlString);

            string? content = await _projectManager.Object.GetMetadataAsync(purl);

            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            Assert.AreEqual(expectedToHaveSecurityHolding, _projectManager.Object.PackageConsideredMalicious(root));
        }
        
        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "2019-07-19T02:28:46.584")]
        [DataRow("pkg:npm/%40angular/core@13.2.5", "2022-03-02T18:25:31.169")]
        [DataRow("pkg:npm/ds-modal@0.0.2", "2018-08-09T07:24:06.206")]
        [DataRow("pkg:npm/monorepolint@0.4.0", "2019-08-07T16:20:53.525")]
        [DataRow("pkg:npm/rly-cli@0.0.2", "2022-03-08T17:26:27.219")]
        [DataRow("pkg:npm/example@0.0.0", "2022-08-10T21:35:38.278")]
        [DataRow("pkg:npm/example@0.0.1")] // No time property in the json for this version
        public async Task GetPublishedAtSucceeds(string purlString, string? expectedTime = null)
        {
            PackageURL purl = new(purlString);
            DateTime? time = await _projectManager.Object.GetPublishedAtUtcAsync(purl, useCache: false);

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
        [DataRow("pkg:npm/lodash@4.17.15", "2012-04-23T16:37:11.912")]
        [DataRow("pkg:npm/%40angular/core@13.2.5", "2016-04-28T04:23:30.108")]
        [DataRow("pkg:npm/ds-modal@0.0.2", "2018-08-06T12:04:34.792")]
        [DataRow("pkg:npm/monorepolint@0.4.0", "2018-12-19T23:29:18.197")]
        [DataRow("pkg:npm/rly-cli@0.0.2", "2022-03-04T05:57:01.108")]
        public async Task GetCreatedAtSucceeds(string purlString, string? expectedTime = null)
        {
            PackageURL purl = new(purlString);
            DateTime? time = await _projectManager.Object.GetPackageCreatedAtUtcAsync(purl, useCache: false);
            Assert.AreEqual(DateTime.Parse(expectedTime), time);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "https://registry.npmjs.org/lodash/-/lodash-4.17.15.tgz")]
        [DataRow("pkg:npm/%40angular/core@13.2.5", "https://registry.npmjs.org/%40angular/core/-/core-13.2.5.tgz")]
        [DataRow("pkg:npm/ds-modal@0.0.2", "https://registry.npmjs.org/ds-modal/-/ds-modal-0.0.2.tgz")]
        [DataRow("pkg:npm/monorepolint@0.4.0", "https://registry.npmjs.org/monorepolint/-/monorepolint-0.4.0.tgz")]
        [DataRow("pkg:npm/example@0.0.0", "https://registry.npmjs.org/example/-/example-0.0.0.tgz")]
        [DataRow("pkg:npm/rly-cli@0.0.2", "https://registry.npmjs.org/rly-cli/-/rly-cli-0.0.2.tgz")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUri)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<NPMProjectManager.NPMArtifactType>> uris = _projectManager.Object.GetArtifactDownloadUris(purl).ToList();

            Assert.AreEqual(expectedUri, uris.First().Uri.AbsoluteUri);
            Assert.AreEqual(".tgz", uris.First().Extension);
            Assert.AreEqual(NPMProjectManager.NPMArtifactType.Tarball, uris.First().Type);
            Assert.IsTrue(await _projectManager.Object.UriExistsAsync(uris.First().Uri));
        }
        
        [DataTestMethod]
        [DataRow("jdalton", "pkg:npm/lodash")]
        [DataRow("microsoft", "pkg:npm/%40microsoft/rush")]
        [DataRow("azure", "pkg:npm/%40azure/cosmos")]
        [DataRow("azure", "pkg:npm/%40azure/graph")]
        public async Task GetPackagesFromOwnerAsyncSucceeds_Async(string owner, string expectedPackage)
        {
            NPMProjectManager projectManager = new(".");

            List<PackageURL> packages = await projectManager.GetPackagesFromOwnerAsync(owner).ToListAsync();

            packages.Should().OnlyHaveUniqueItems();
            packages.Select(p => p.ToString()).Should().Contain(expectedPackage);
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
