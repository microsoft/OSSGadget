// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CodeAnalysis.Sarif;
    using Microsoft.CST.OpenSource.Extensions;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageActions;
    using Moq;
    using oss;
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
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CargoProjectManagerTests
    {
        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://raw.githubusercontent.com/rust-lang/crates.io-index/master/ra/nd/rand", Resources.cargo_rand },
            { "https://crates.io/api/v1/crates/rand", Resources.cargo_rand_json },
            { "https://static.crates.io/rss/crates/rand.xml", Resources.cargo_rss_rand_xml },
        }.ToImmutableDictionary();

        private readonly IDictionary<string, bool> _retryTestsPackages = new Dictionary<string, bool>()
        {
            { "https://crates.io/api/v1/crates/a-mazed*", true },
            { "https://static.crates.io/crates/a-mazed*", true },
            { "https://raw.githubusercontent.com/rust-lang/crates.io-index/master/a-/ma/a-mazed", true},
            { "https://crates.io/api/v1/crates/A2VConverter*", false},
            { "https://raw.githubusercontent.com/rust-lang/crates.io-index/master/A2/VC/A2VConverter", false},
        }.ToImmutableDictionary();

        private readonly CargoProjectManager _projectManager;
        private readonly IHttpClientFactory _httpFactory;

        public CargoProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();

            MockHttpMessageHandler mockHttp = new();

            foreach ((string Url, bool ShouldSucceedAfterRetry) in _retryTestsPackages)
            {
                ConfigureMockHttpForRetryMechanismTests(mockHttp, Url, ShouldSucceedAfterRetry);
            }

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;

            _projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/a-mazed@0.1.0")]
        public async Task GetMetadataAsyncRetries500InternalServerError(string purlString)
        {
            PackageURL purl = new(purlString);
            string? sampleResult = await new StringContent(JsonSerializer.Serialize(new { Name = "sampleName", Content = "sampleContent" }), Encoding.UTF8, "application/json").ReadAsStringAsync();

            string? result = await _projectManager.GetMetadataAsync(purl, useCache: false);

            Assert.IsNotNull(result);
            Assert.AreEqual(sampleResult, result);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/a-mazed@0.1.0")]
        public async Task DownloadVersionAsyncRetries500InternalServerError(string purlString)
        {
            PackageURL purl = new(purlString);

            IEnumerable<string> downloadedPath = await _projectManager.DownloadVersionAsync(purl, doExtract: true, cached: false);

            Assert.IsFalse(downloadedPath.IsEmptyEnumerable());
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/a-mazed@0.1.0")]
        public async Task EnumerateVersionsAsyncRetries500InternalServerError(string purlString)
        {
            PackageURL purl = new(purlString);

            IEnumerable<string> versionsList = await _projectManager.EnumerateVersionsAsync(purl);

            Assert.IsNotNull(versionsList);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/A2VConverter@0.1.1")]
        public async Task GetMetadataAsyncThrowsExceptionAfterMaxRetries(string purlString)
        {
            PackageURL purl = new(purlString);

            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => _projectManager.GetPackageMetadataAsync(purl, useCache: false));

            Assert.IsNotNull(exception);
            Assert.AreEqual(exception.StatusCode, HttpStatusCode.InternalServerError);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/A2VConverter@0.1.1")]
        public async Task GetMetadataAsyncThrowsExceptionIfUsageOfRateLimitedApiIsDisabled(string purlString)
        {
            PackageURL purl = new(purlString);

            var _projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory, allowUseOfRateLimitedRegistryAPIs: false);
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _projectManager.GetMetadataAsync(purl, useCache: false));

            Assert.IsNotNull(exception);
            Assert.AreEqual(exception.Message, "Rate-limited API is disabled. Crates.io does not have a non-rate-limited API defined to fetch metadata.  See https://crates.io/data-access.");
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/A2VConverter@0.1.1")]
        public async Task GetPackageMetadataAsyncThrowsExceptionIfUsageOfRateLimitedApiIsDisabled(string purlString)
        {
            PackageURL purl = new(purlString);

            var _projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory, allowUseOfRateLimitedRegistryAPIs: false);
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _projectManager.GetPackageMetadataAsync(purl, useCache: false));

            Assert.IsNotNull(exception);
            Assert.AreEqual(exception.Message, "Rate-limited API is disabled. Crates.io does not have a non-rate-limited API defined to fetch metadata.  See https://crates.io/data-access.");
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/A2VConverter@0.1.1")]
        public async Task DownloadVersionAsyncThrowsExceptionAfterMaxRetries(string purlString)
        {
            PackageURL purl = new(purlString);

            IEnumerable<string> downloadedPath = await _projectManager.DownloadVersionAsync(purl, doExtract: true, cached: false);

            Assert.IsTrue(downloadedPath.IsEmptyEnumerable());
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/A2VConverter@0.1.1")]
        public async Task EnumerateVersionsAsyncThrowsExceptionAfterMaxRetries(string purlString)
        {
            PackageURL purl = new(purlString);

            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => _projectManager.EnumerateVersionsAsync(purl));

            Assert.IsNotNull(exception);
            Assert.AreEqual(exception.StatusCode, HttpStatusCode.InternalServerError);
        }


        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.8.5", "https://static.crates.io/crates/rand/0.8.5/download")]
        [DataRow("pkg:cargo/quote@1.0.21", "https://static.crates.io/crates/quote/1.0.21/download")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUri)
        {
            PackageURL purl = new(purlString);
            List<ArtifactUri<CargoProjectManager.CargoArtifactType>> uris = await _projectManager.GetArtifactDownloadUrisAsync(purl).ToListAsync();

            Assert.AreEqual(expectedUri, uris.First().Uri.AbsoluteUri);
            Assert.AreEqual(CargoProjectManager.CargoArtifactType.Tarball, uris.First().Type);
        }

        //Test fails in pipeline
        [Ignore]
        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3", 68, "0.8.5")]
        public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
        {
            PackageURL purl = new(purlString);
            List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, useCache: false)).ToList();

            Assert.AreEqual(count, versions.Count);
            Assert.AreEqual(latestVersion, versions.First());
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.9.0", "Mon, 27 Jan 2025 13:38:34 +0000")]
        public async Task GetPublishedTimeStampFromRSSFeedSucceeds(string purlString, string expectedTime)
        {
            PackageURL purl = new(purlString);
            DateTime? dateTime = await _projectManager.GetPublishedAtUtcAsync(purl, useCache: false);
            Assert.IsNotNull(dateTime);
            Assert.AreEqual(DateTime.Parse(expectedTime).ToUniversalTime(), dateTime);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3", "2020-01-10T21:46:21.337656+00:00")]
        public async Task GetPublishedTimeStampFromRateLimitedAPIWhenRssFeedReturnsNullAndUsageOfRateLimitedApiIsEnabled(string purlString, string expectedTime)
        {
            PackageURL purl = new(purlString);
            DateTime? dateTime = await _projectManager.GetPublishedAtUtcAsync(purl, useCache: false);
            Assert.IsNotNull(dateTime);
            Assert.AreEqual(DateTime.Parse(expectedTime).ToUniversalTime(), dateTime);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3")]
        public async Task GetPublishedTimeStampReturnsNullWhenRssFeedReturnsNullAndUsageOfRateLimitedApiIsDisabled(string purlString)
        {
            PackageURL purl = new(purlString);
            var _projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory, allowUseOfRateLimitedRegistryAPIs:false);
            DateTime? dateTime = await _projectManager.GetPublishedAtUtcAsync(purl, useCache: false);
            Assert.IsNull(dateTime);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3")]
        public async Task PackageVersionExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsTrue(await _projectManager.PackageVersionExistsAsync(purl, useCache: false));
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.4")]
        public async Task PackageVersionDoesntExistsAsyncSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            Assert.IsFalse(await _projectManager.PackageVersionExistsAsync(purl, useCache: false));
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3")] // Normal package
        public async Task MetadataSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);
            PackageMetadata? metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

            Assert.IsNotNull(metadata);
            Assert.AreEqual(purl.GetFullName(), metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.IsNotNull(metadata.UploadTime);
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.4")] // Does Not Exist package
        public async Task MetadataFails(string purlString)
        {
            PackageURL purl = new(purlString);
            PackageMetadata? metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

            Assert.IsNull(metadata);
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

        private static void ConfigureMockHttpForRetryMechanismTests(MockHttpMessageHandler mockHttp, string url, bool shouldSucceedAfterRetry = true)
        {
            if (shouldSucceedAfterRetry)
            {
                int callCount = 0;
                mockHttp
                    .When(HttpMethod.Get, url)
                    .Respond(_ =>
                    {
                        callCount++;
                        if (callCount == 1) // Fail and return 500 on 1st attempt
                        {
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                        }
                        return new HttpResponseMessage(HttpStatusCode.OK) // Succeed on subsequent attempts
                        {
                            Content = new StringContent(JsonSerializer.Serialize(new { Name = "sampleName", Content = "sampleContent" }), Encoding.UTF8, "application/json")

                        };
                    });
            }
            else
            {
                mockHttp
                   .When(HttpMethod.Get, url)
                   .Respond(HttpStatusCode.InternalServerError);
            }
        }
    }
}
