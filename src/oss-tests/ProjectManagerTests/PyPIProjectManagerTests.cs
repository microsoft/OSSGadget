// Copyright (c) Microsoft Corporation. Licensed under the MIT License.


namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Contracts;
    using Extensions;
    using FluentAssertions;
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
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PyPIProjectManagerTests
    {
        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://pypi.org/pypi/pandas/json", Resources.pandas_json },
            { "https://pypi.org/pypi/pandas/1.4.2/json", Resources.pandas_1_4_2_json },
            { "https://pypi.org/pypi/plotly/json", Resources.plotly_json },
            { "https://pypi.org/pypi/plotly/5.7.0/json", Resources.plotly_5_7_0_json },
            { "https://pypi.org/pypi/requests/json", Resources.requests_json },
            { "https://pypi.org/pypi/requests/2.27.1/json", Resources.requests_2_27_1_json },
        }.ToImmutableDictionary();
        
        private readonly IDictionary<string, string[]> _packageArtifacts = new Dictionary<string, string[]>()
        {
            {
                "pkg:pypi/pandas@1.4.2",
                new[]
                {
                    "https://files.pythonhosted.org/packages/a4/ca/a1c076db546f41d5624c883ec65670180ae8131867141aef1d9c214e3782/pandas-1.4.2-cp310-cp310-macosx_10_9_universal2.whl",
                    "https://files.pythonhosted.org/packages/89/1c/05c3233ee135d0626f2430125115a0728738627f1c65ceea6c75ea99e657/pandas-1.4.2-cp310-cp310-macosx_10_9_x86_64.whl",
                    "https://files.pythonhosted.org/packages/65/0f/7be3e15ab01448a59416eb7b5c4b1d444090bfd2737df21062b54bfd1a43/pandas-1.4.2-cp310-cp310-macosx_11_0_arm64.whl",
                    "https://files.pythonhosted.org/packages/76/e2/8514d284c396c0ffec69b8477d8d115dc9f32763a2e02394a418cb9adf4d/pandas-1.4.2-cp310-cp310-manylinux_2_17_aarch64.manylinux2014_aarch64.whl",
                    "https://files.pythonhosted.org/packages/ab/80/c3def79fb1c8a4c5a91d1efa5f611e81529bbab947ac1e9fcd736fd4dcc3/pandas-1.4.2-cp310-cp310-manylinux_2_17_x86_64.manylinux2014_x86_64.whl",
                    "https://files.pythonhosted.org/packages/aa/4f/b42d0a158f4777d4f60269eef51ae13b41821119b13fe8972a926b6558a1/pandas-1.4.2-cp310-cp310-win_amd64.whl",
                    "https://files.pythonhosted.org/packages/9c/8b/2b25983b0f0abbb6c634fd72cea17276e1736556582dd614f0ef6712c361/pandas-1.4.2-cp38-cp38-macosx_10_9_universal2.whl",
                    "https://files.pythonhosted.org/packages/47/a5/79156a83c133b5d049a38f444e11eacabab8b3ad00814d8c6811fe9850e2/pandas-1.4.2-cp38-cp38-macosx_10_9_x86_64.whl",
                    "https://files.pythonhosted.org/packages/bc/3e/bb3eecf53d94fde7d1b74631b27d64e88b168d30dc379b0804c9ebfccdda/pandas-1.4.2-cp38-cp38-macosx_11_0_arm64.whl",
                    "https://files.pythonhosted.org/packages/8c/26/1cd0728c23084834c2460118b2e7306e9aea9454694bb33390c0d3616890/pandas-1.4.2-cp38-cp38-manylinux_2_17_aarch64.manylinux2014_aarch64.whl",
                    "https://files.pythonhosted.org/packages/12/07/e82b5defa695f09dd0ab1aecda886eb1c1aa6807c34ac3a0d691dc64503c/pandas-1.4.2-cp38-cp38-manylinux_2_17_x86_64.manylinux2014_x86_64.whl",
                    "https://files.pythonhosted.org/packages/f8/29/c56097eb160176e2c4dc32f3b5a8ab300ccf394ba794938552591d8873ea/pandas-1.4.2-cp38-cp38-win32.whl",
                    "https://files.pythonhosted.org/packages/9b/93/e937ef7dc2d712820e4aafdc152d575979adbd192b0ad80f78a28e1f56f3/pandas-1.4.2-cp38-cp38-win_amd64.whl",
                    "https://files.pythonhosted.org/packages/cb/80/fd11b19936d203cc7b4b7150f1ec950ef5472f391e7c7abfc48d2d736f8d/pandas-1.4.2-cp39-cp39-macosx_10_9_universal2.whl",
                    "https://files.pythonhosted.org/packages/46/70/773d7835784d1f91226f4ab2543b1d9952a5c3c87638e2665a7cc984dd5f/pandas-1.4.2-cp39-cp39-macosx_10_9_x86_64.whl",
                    "https://files.pythonhosted.org/packages/a9/33/0c2c716f37c1b630ad51a6fb46a850d675d8a18eb35a7bdb0b2897566c89/pandas-1.4.2-cp39-cp39-macosx_11_0_arm64.whl",
                    "https://files.pythonhosted.org/packages/4b/db/050b07aa97661da33fa57f2b8c3aa6a7e10ad6c2e6136cfb0bdc7213307f/pandas-1.4.2-cp39-cp39-manylinux_2_17_aarch64.manylinux2014_aarch64.whl",
                    "https://files.pythonhosted.org/packages/35/ad/616c27cade647c2a1513343c72c095146cf3e7a72ace6582574a334fb525/pandas-1.4.2-cp39-cp39-manylinux_2_17_x86_64.manylinux2014_x86_64.whl",
                    "https://files.pythonhosted.org/packages/53/e5/4e5193bb7d416a5cd258d4e9d8cd47b38ef2533d4048e7fd32dab083690d/pandas-1.4.2-cp39-cp39-win32.whl",
                    "https://files.pythonhosted.org/packages/3a/7a/695bfc4a641ab3867de6b43535809a3dace99d1a0a9245b629b8b98d02e9/pandas-1.4.2-cp39-cp39-win_amd64.whl",
                    "https://files.pythonhosted.org/packages/5a/ac/b3b9aa2318de52e40c26ae7b9ce6d4e9d1bcdaf5da0899a691642117cf60/pandas-1.4.2.tar.gz",
                }
            },
            { 
                "pkg:pypi/plotly@5.7.0", 
                new[]
                {
                    "https://files.pythonhosted.org/packages/cb/70/53e9f634e6d3aedbe2e99deca846226385d5d845212171e227e2502fb240/plotly-5.7.0-py2.py3-none-any.whl",
                    "https://files.pythonhosted.org/packages/aa/92/69d702f337dd179525562631c2e2113504a4ea5b4183f993f372a9b72e3c/plotly-5.7.0.tar.gz",
                }
            },
            {
                "pkg:pypi/requests@2.27.1",
                new[]
                {
                    "https://files.pythonhosted.org/packages/2d/61/08076519c80041bc0ffa1a8af0cbd3bf3e2b62af10435d269a9d0f40564d/requests-2.27.1-py2.py3-none-any.whl",
                    "https://files.pythonhosted.org/packages/60/f3/26ff3767f099b73e0efa138a9998da67890793bfa475d8278f84a30fec77/requests-2.27.1.tar.gz",
                }
            },
        }.ToImmutableDictionary();

        private readonly PyPIProjectManager _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        public PyPIProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            
            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            mockHttp.When(HttpMethod.Get, "https://pypi.org/pypi/plotly/3.7.1/json").Respond(HttpStatusCode.OK);
            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new PyPIProjectManager(".", new NoOpPackageActions(), _httpFactory);
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2", "Powerful data structures for data analysis, time series, and statistics")]
        [DataRow("pkg:pypi/plotly@5.7.0", "An open-source, interactive data visualization library for Python")]
        [DataRow("pkg:pypi/requests@2.27.1", "Python HTTP for Humans.")]
        public async Task MetadataSucceeds(string purlString, string? description = null)
        {
            PackageURL purl = new(purlString);
            PackageMetadata metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

            Assert.AreEqual(purl.Name, metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
        }
        
        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2", "2022-04-02T10:32:27")]
        [DataRow("pkg:pypi/plotly@5.7.0", "2022-04-05T16:26:03")]
        [DataRow("pkg:pypi/requests@2.27.1", "2022-01-05T15:40:49")]
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
        [DataRow("pkg:pypi/pandas@1.4.2", 86, "1.4.2")]
        [DataRow("pkg:pypi/plotly@3.7.1", 276, "5.7.0")]
        [DataRow("pkg:pypi/requests@2.27.1", 145, "2.27.1")]
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
            List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, useCache: false)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2")]
        [DataRow("pkg:pypi/plotly@3.7.1")]
        [DataRow("pkg:pypi/requests@2.27.1")]
        public async Task PackageVersionExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsTrue(await _projectManager.PackageVersionExistsAsync(purl, useCache: false));
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2", "2022-04-02T10:32:27")]
        [DataRow("pkg:pypi/plotly@5.7.0", "2022-04-05T16:26:03")]
        [DataRow("pkg:pypi/requests@2.27.1", "2022-01-05T15:40:49")]
        public async Task GetPublishedAtUtcSucceeds(string purlString, string? expectedTime = null)
        {
            PackageURL purl = new(purlString);
            DateTime? time = await _projectManager.GetPublishedAtUtcAsync(purl, useCache: false);

            if (expectedTime == null)
            {
                Assert.IsNull(time);
            }
            else
            {
                Assert.AreEqual(DateTime.Parse(expectedTime).ToUniversalTime(), time);
            }
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/pandas", true)]
        [DataRow("pkg:pypi/plotly@3.7.1", true)]
        [DataRow("pkg:pypi/notarealpackage", false)]
        public async Task DetailedPackageExistsAsync_WorksAsExpected(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);
            IPackageExistence existence = await _projectManager.DetailedPackageExistsAsync(purl, useCache: false);

            Assert.AreEqual(exists, existence.Exists);
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2", true)]
        [DataRow("pkg:pypi/pandas@12.34.56.78", false)]
        [DataRow("pkg:pypi/plotly@5.7.0", true)]
        [DataRow("pkg:pypi/requests@2.27.1", true)]
        [DataRow("pkg:pypi/notarealpackage@0.0.0", false)]
        public async Task DetailedPackageVersionExistsAsync_WorksAsExpected(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);
            IPackageExistence existence = await _projectManager.DetailedPackageVersionExistsAsync(purl, useCache: false);

            Assert.AreEqual(exists, existence.Exists);
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2")]
        [DataRow("pkg:pypi/plotly@5.7.0")]
        [DataRow("pkg:pypi/requests@2.27.1")]
        public void GetArtifactDownloadUrisSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<PyPIProjectManager.PyPIArtifactType>> uris = _projectManager.GetArtifactDownloadUris(purl).ToList();

            foreach (ArtifactUri<PyPIProjectManager.PyPIArtifactType> artifactUri in uris)
            {
                artifactUri.Uri.AbsoluteUri.Should().BeOneOf(_packageArtifacts[purlString]);
                artifactUri.Type.Should().Be(ExtensionToType(artifactUri.Uri));
            }
        }
        
        [DataTestMethod]
        [DataRow("pkg:pypi/pandas@1.4.2")]
        [DataRow("pkg:pypi/plotly@5.7.0")]
        [DataRow("pkg:pypi/requests@2.27.1")]
        public async Task GetArtifactDownloadUrisAsync_SucceedsAsync(string purlString)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<PyPIProjectManager.PyPIArtifactType>> uris = await _projectManager.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            foreach (ArtifactUri<PyPIProjectManager.PyPIArtifactType> artifactUri in uris)
            {
                artifactUri.Uri.AbsoluteUri.Should().BeOneOf(_packageArtifacts[purlString]);
                artifactUri.Type.Should().Be(ExtensionToType(artifactUri.Uri));
            }
        }
        
        [DataTestMethod]
        [DataRow("microsoft", "pkg:pypi/azdev")]
        public async Task GetPackagesFromOwnerAsyncSucceeds_Async(string owner, string expectedPackage)
        {
            PyPIProjectManager projectManager = new(".");

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

            if (url.EndsWith("/json"))
            {
                string newUrlNoJson = url.Substring(0, url.Length - "/json".Length);

                int pos = newUrlNoJson.LastIndexOf("/", StringComparison.Ordinal) + 1;
                string packageName = newUrlNoJson.Substring(pos, newUrlNoJson.Length - pos).ToLowerInvariant();

                // Mock the call to get the artifact tarball.
                httpMock
                    .When(HttpMethod.Get,
                        $"https://pypi.org/packages/source/{packageName[0]}/{packageName}/{packageName}-*.tar.gz")
                    .Respond(statusCode);
            }
        }

        /// <summary>
        /// Helper method to get the <see cref="PyPIProjectManager.PyPIArtifactType"/> from a <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to use.</param>
        /// <returns>The <see cref="PyPIProjectManager.PyPIArtifactType"/> from the <see cref="Uri"/>.</returns>
        private static PyPIProjectManager.PyPIArtifactType ExtensionToType(Uri uri) => uri.GetExtension() switch
        {
            ".tar.gz" => PyPIProjectManager.PyPIArtifactType.Tarball,
            ".whl" => PyPIProjectManager.PyPIArtifactType.Wheel,
            _ => PyPIProjectManager.PyPIArtifactType.Unknown
        };
    }
}
