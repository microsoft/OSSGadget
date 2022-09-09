// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Extensions;
    using FluentAssertions;
    using Model;
    using Model.Enums;
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
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NPMProjectManagerTests
    {
        private static readonly IDictionary<string, (PackageVersionExistence packageVersionExistence, bool versionPulled, bool packagePulled, bool securityRemoved)> _packageVersionExistence = new Dictionary<string, (PackageVersionExistence packageVersionExistence, bool versionPulled, bool packagePulled, bool securityRemoved)>()
        {
            { "currently_exists",
                (new PackageVersionExistence(true, true, true, true),
                    versionPulled: false,
                    packagePulled: false,
                    securityRemoved: false)
            },
            { "version_existed",
                (new PackageVersionExistence(false, true, true, true, new [] { PackageDoesNotExistReason.VersionUnpublished }),
                    versionPulled: true,
                    packagePulled: false,
                    securityRemoved: false)
            },
            { "version_never_existed",
                (new PackageVersionExistence(false, false, true, true),
                    versionPulled: false,
                    packagePulled: false,
                    securityRemoved: false)
            },
            { "package_removed_version_did_exist",
                (new PackageVersionExistence(false, true, false, true, 
                        new []
                        {
                            PackageDoesNotExistReason.VersionUnpublished,
                            PackageDoesNotExistReason.PackageUnpublished,
                        }),
                    versionPulled: true,
                    packagePulled: true,
                    securityRemoved: false)
            },
            { "package_removed_version_never_existed",
                (new PackageVersionExistence(false, false, false, true, new [] { PackageDoesNotExistReason.PackageUnpublished }),
                    versionPulled: false,
                    packagePulled: true,
                    securityRemoved: false)
            },
            { "package_never_existed",
                (new PackageVersionExistence(false, false, false, false),
                    versionPulled: false,
                    packagePulled: false,
                    securityRemoved: false)
            },
            { "package_removed_for_security",
                (new PackageVersionExistence(false, false, false, true, new []
                    {
                        PackageDoesNotExistReason.RemovedForSecurity
                    }),
                    versionPulled: false,
                    packagePulled: false,
                    securityRemoved: true)
            },
            { "package_removed_for_security_version_existed",
                (new PackageVersionExistence(false, true, false, true, new []
                    {
                        PackageDoesNotExistReason.VersionUnpublished,
                        PackageDoesNotExistReason.RemovedForSecurity
                    }),
                    versionPulled: true,
                    packagePulled: false,
                    securityRemoved: true)
            },
        };
        
        private static readonly IDictionary<string, (PackageExistence packageExistence, bool pulled, bool securityRemoved)> _packageExistence = new Dictionary<string, (PackageExistence packageExistence, bool pulled, bool securityRemoved)>()
        {
            { "currently_exists",
                (new PackageExistence(true, true),
                    pulled: false,
                    securityRemoved: false)
            },
            { "never_existed",
                (new PackageExistence(false, false),
                    pulled: false,
                    securityRemoved: false)
            },
            { "did_exist",
                (new PackageExistence(false, true, new [] { PackageDoesNotExistReason.PackageUnpublished }),
                    pulled: true,
                    securityRemoved: false)
            },
            { "security_removed",
                (new PackageExistence(false, true, new [] { PackageDoesNotExistReason.RemovedForSecurity }),
                    pulled: false,
                    securityRemoved: true)
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
            { "https://registry.npmjs.org/example", Resources.minimum_json_json },
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
        [DataRow("pkg:npm/lodash@4.17.15", 114, "2019-07-19T02:28:46.584Z")]
        [DataRow("pkg:npm/%40angular/core@13.2.5", 567, "2022-03-02T18:25:31.169Z")]
        [DataRow("pkg:npm/ds-modal@0.0.2", 8, "2018-08-09T07:24:06.206Z")]
        [DataRow("pkg:npm/monorepolint@0.4.0", 88, "2019-08-07T16:20:53.525Z")]
        [DataRow("pkg:npm/example@0.0.0", 1, "2022-08-10T21:35:38.278Z")]
        [DataRow("pkg:npm/rly-cli@0.0.2", 4, "2022-03-08T17:26:27.219Z")]
        public async Task EnumerateVersionsWithPublishTimeSucceeds(string purlString, int count, string versionPublishTime)
        {
            PackageURL purl = new(purlString);
            IDictionary<string, DateTime> versions = await _projectManager.Object.EnumerateVersionsWithPublishTimeAsync(purl, useCache: false);

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(DateTime.Parse(versionPublishTime), versions[purl.Version]);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15")]
        [DataRow("pkg:npm/%40angular/core@13.2.5")]
        [DataRow("pkg:npm/ds-modal@0.0.2")]
        [DataRow("pkg:npm/monorepolint@0.4.0")]
        [DataRow("pkg:npm/example@0.0.0")]
        [DataRow("pkg:npm/rly-cli@0.0.2")]
        [DataRow("pkg:npm/lodash.js@0.0.1-security")]
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
        [DataRow("currently_exists")] // The package version currently exists
        [DataRow("version_existed")] // The package version was pulled from the registry
        [DataRow("version_never_existed")] // The package version never existed
        [DataRow("package_removed_version_did_exist")] // The package itself was pulled and the version did exist
        [DataRow("package_removed_version_never_existed")] // The package itself was pulled and the version never existed
        [DataRow("package_never_existed")] // The package never existed
        [DataRow("package_removed_for_security")] // The package was removed for security
        [DataRow("package_removed_for_security_version_existed")] // The package was removed for security, but the version did exist prior to the package being removed
        public async Task DetailedPackageVersionExistsAsyncSucceeds(
            string key)
        {
            PackageVersionExistence expectedPackageVersionExistence = _packageVersionExistence[key].packageVersionExistence;
            bool versionPulled = _packageVersionExistence[key].versionPulled;
            bool packagePulled = _packageVersionExistence[key].packagePulled;
            bool securityRemoved = _packageVersionExistence[key].securityRemoved;
            
            _projectManager
                .Setup(p => 
                    p.PackageVersionExistsAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(expectedPackageVersionExistence.Exists);
            _projectManager
                .Setup(p => 
                    p.PackageExistsAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(expectedPackageVersionExistence.PackageExists);
            _projectManager
                .Setup(p => 
                    p.PackageVersionPulled(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(versionPulled);
            _projectManager
                .Setup(p => 
                    p.PackagePulled(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(packagePulled);
            _projectManager
                .Setup(p => 
                    p.PackageRemovedForSecurity(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(securityRemoved);

            PackageVersionExistence packageVersionExistenceResponse =
                await _projectManager.Object.DetailedPackageVersionExistsAsync(new PackageURL("pkg:npm/example@0.0.0"),
                    useCache: false);

            packageVersionExistenceResponse.Should().BeEquivalentTo(expectedPackageVersionExistence);
        }
        
        [DataTestMethod]
        [DataRow("currently_exists")] // The package currently exists
        [DataRow("never_existed")] // The package never existed
        [DataRow("did_exist")] // The package existed but was removed
        [DataRow("security_removed")] // The package was removed for security reasons
        public async Task DetailedPackageExistsAsyncSucceeds(
            string key)
        {
            PackageExistence expectedPackageExistence = _packageExistence[key].packageExistence;
            bool pulled = _packageExistence[key].pulled;
            bool securityRemoved = _packageExistence[key].securityRemoved;

            _projectManager
                .Setup(p => 
                    p.PackageExistsAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(expectedPackageExistence.Exists);
            _projectManager
                .Setup(p => 
                    p.PackagePulled(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(pulled);
            _projectManager
                .Setup(p => 
                    p.PackageRemovedForSecurity(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(securityRemoved);

            PackageExistence packageExistenceResponse =
                await _projectManager.Object.DetailedPackageExistsAsync(new PackageURL("pkg:npm/example@0.0.0"),
                    useCache: false);

            packageExistenceResponse.Should().BeEquivalentTo(expectedPackageExistence);
        }
        
        [DataTestMethod]
        [DataRow("pkg:npm/%40somosme/webflowutils@1.0.0")]
        [DataRow("pkg:npm/%40somosme/webflowutils@1.2.3", false)]
        [DataRow("pkg:npm/%40achievementify/client@0.2.1")]
        [DataRow("pkg:npm/%40achievementify/clients@0.2.3", false)]
        public async Task PackageVersionPulledAsync(string purlString, bool expectedPulled = true)
        {
            PackageURL purl = new(purlString);

            Assert.AreEqual(expectedPulled, await _projectManager.Object.PackageVersionPulled(purl, useCache: false));
        }
        
        [DataTestMethod]
        [DataRow("pkg:npm/lodash.js")]
        [DataRow("pkg:npm/lodash.js@1.0.0")]
        [DataRow("pkg:npm/lodash", false)]
        public async Task PackageSecurityHoldingAsync(string purlString, bool expectedToHaveSecurityHolding = true)
        {
            PackageURL purl = new(purlString);

            Assert.AreEqual(expectedToHaveSecurityHolding, await _projectManager.Object.PackageRemovedForSecurity(purl, useCache: false));
        }
        
        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "2019-07-19T02:28:46.584Z")]
        [DataRow("pkg:npm/%40angular/core@13.2.5", "2022-03-02T18:25:31.169Z")]
        [DataRow("pkg:npm/ds-modal@0.0.2", "2018-08-09T07:24:06.206Z")]
        [DataRow("pkg:npm/monorepolint@0.4.0", "2019-08-07T16:20:53.525Z")]
        [DataRow("pkg:npm/rly-cli@0.0.2", "2022-03-08T17:26:27.219Z")]
        [DataRow("pkg:npm/example@0.0.0", "2022-08-10T21:35:38.278Z")]
        [DataRow("pkg:npm/example@0.0.1")] // No time property in the json for this version
        public async Task GetPublishedAtSucceeds(string purlString, string? expectedTime = null)
        {
            PackageURL purl = new(purlString);
            DateTime? time = await _projectManager.Object.GetPublishedAtAsync(purl, useCache: false);

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
