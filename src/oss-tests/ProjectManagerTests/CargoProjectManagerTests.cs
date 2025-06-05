// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Extensions;
using Microsoft.CST.OpenSource.Model;
using Microsoft.CST.OpenSource.PackageActions;
using Microsoft.CST.OpenSource.PackageManagers;
using Moq;
using oss;
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
using System.Threading.Tasks;

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

    [Theory]
    [InlineData("pkg:cargo/a-mazed@0.1.0")]
    public async Task GetMetadataAsyncRetries500InternalServerError(string purlString)
    {
        PackageURL purl = new(purlString);
        string? sampleResult = await new StringContent(JsonSerializer.Serialize(new { Name = "sampleName", Content = "sampleContent" }), Encoding.UTF8, "application/json").ReadAsStringAsync();

        string? result = await _projectManager.GetMetadataAsync(purl, useCache: false);

        result.Should().NotBeNull();
        result.Should().Be(sampleResult);
    }

    [Theory]
    [InlineData("pkg:cargo/a-mazed@0.1.0")]
    public async Task DownloadVersionAsyncRetries500InternalServerError(string purlString)
    {
        PackageURL purl = new(purlString);

        IEnumerable<string> downloadedPath = await _projectManager.DownloadVersionAsync(purl, doExtract: true, cached: false);

        downloadedPath.IsEmptyEnumerable().Should().BeFalse();
    }

    [Theory]
    [InlineData("pkg:cargo/a-mazed@0.1.0")]
    public async Task EnumerateVersionsAsyncRetries500InternalServerError(string purlString)
    {
        PackageURL purl = new(purlString);

        IEnumerable<string> versionsList = await _projectManager.EnumerateVersionsAsync(purl);

        versionsList.Should().NotBeNull();
    }

    [Theory]
    [InlineData("pkg:cargo/A2VConverter@0.1.1")]
    public async Task GetMetadataAsyncThrowsExceptionAfterMaxRetries(string purlString)
    {
        PackageURL purl = new(purlString);

        var action = async () => await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

        await action.Should().ThrowAsync<HttpRequestException>()
            .Where(e => e.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Theory]
    [InlineData("pkg:cargo/A2VConverter@0.1.1")]
    public async Task GetMetadataAsyncThrowsExceptionIfUsageOfRateLimitedApiIsDisabled(string purlString)
    {
        const string message = "Rate-limited API is disabled. Crates.io does not have a non-rate-limited API defined to fetch metadata.  See https://crates.io/data-access.";
        PackageURL purl = new(purlString);
        var projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory, allowUseOfRateLimitedRegistryAPIs: false);

        var action = async () => await projectManager.GetMetadataAsync(purl, useCache: false);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message == message);
    }

    [Theory]
    [InlineData("pkg:cargo/A2VConverter@0.1.1")]
    public async Task GetPackageMetadataAsyncThrowsExceptionIfUsageOfRateLimitedApiIsDisabled(string purlString)
    {
        const string message = "Rate-limited API is disabled. Crates.io does not have a non-rate-limited API defined to fetch metadata.  See https://crates.io/data-access.";
        PackageURL purl = new(purlString);
        var projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory, allowUseOfRateLimitedRegistryAPIs: false);

        var action = async () => await projectManager.GetPackageMetadataAsync(purl, useCache: false);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message == message);
    }

    [Theory]
    [InlineData("pkg:cargo/A2VConverter@0.1.1")]
    public async Task DownloadVersionAsyncThrowsExceptionAfterMaxRetries(string purlString)
    {
        PackageURL purl = new(purlString);

        IEnumerable<string> downloadedPath = await _projectManager.DownloadVersionAsync(purl, doExtract: true, cached: false);

        downloadedPath.IsEmptyEnumerable().Should().BeTrue();
    }

    [Theory]
    [InlineData("pkg:cargo/A2VConverter@0.1.1")]
    public async Task EnumerateVersionsAsyncThrowsExceptionAfterMaxRetries(string purlString)
    {
        PackageURL purl = new(purlString);

        var action = async () => await _projectManager.EnumerateVersionsAsync(purl);

        await action.Should().ThrowAsync<HttpRequestException>()
            .Where(e => e.StatusCode == HttpStatusCode.InternalServerError);
    }


    [Theory]
    [InlineData("pkg:cargo/rand@0.8.5", "https://static.crates.io/crates/rand/0.8.5/download")]
    [InlineData("pkg:cargo/quote@1.0.21", "https://static.crates.io/crates/quote/1.0.21/download")]
    public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUri)
    {
        PackageURL purl = new(purlString);
        
        List<ArtifactUri<CargoProjectManager.CargoArtifactType>> uris = await _projectManager.GetArtifactDownloadUrisAsync(purl).ToListAsync();

        ArtifactUri<CargoProjectManager.CargoArtifactType> artifactUri = uris.First();
        artifactUri.Uri.AbsoluteUri.Should().Be(expectedUri);
        artifactUri.Type.Should().Be(CargoProjectManager.CargoArtifactType.Tarball);
    }

    [Fact(Skip = "Test fails in pipeline")]
    public async Task EnumerateVersionsSucceeds()
    {
        string purlString = "pkg:cargo/rand@0.7.3";
        int count = 68;
        string latestVersion = "0.8.5";
        PackageURL purl = new(purlString);

        List<string> versions = (await _projectManager.EnumerateVersionsAsync(purl, useCache: false)).ToList();

        versions.Should().HaveCount(count);
        latestVersion.Should().Be(versions.First());
    }

    [Theory]
    [InlineData("pkg:cargo/rand@0.9.0", "Mon, 27 Jan 2025 13:38:34 +0000")]
    public async Task GetPublishedTimeStampFromRSSFeedSucceeds(string purlString, string expectedTime)
    {
        DateTime expectedDateTime = DateTime.Parse(expectedTime).ToUniversalTime();
        PackageURL purl = new(purlString);

        DateTime? dateTime = await _projectManager.GetPublishedAtUtcAsync(purl, useCache: false);

        dateTime.Should().NotBeNull();        
        dateTime.Should().Be(expectedDateTime);
    }

    [Theory]
    [InlineData("pkg:cargo/rand@0.7.3", "2020-01-10T21:46:21.337656+00:00")]
    public async Task GetPublishedTimeStampFromRateLimitedAPIWhenRssFeedReturnsNullAndUsageOfRateLimitedApiIsEnabled(string purlString, string expectedTime)
    {
        DateTime expectedDateTime = DateTime.Parse(expectedTime).ToUniversalTime();
        PackageURL purl = new(purlString);

        DateTime? dateTime = await _projectManager.GetPublishedAtUtcAsync(purl, useCache: false);

        dateTime.Should().NotBeNull();
        dateTime.Should().Be(expectedDateTime);
    }

    [Theory]
    [InlineData("pkg:cargo/rand@0.7.3")]
    public async Task GetPublishedTimeStampReturnsNullWhenRssFeedReturnsNullAndUsageOfRateLimitedApiIsDisabled(string purlString)
    {
        PackageURL purl = new(purlString);
        var _projectManager = new CargoProjectManager(".", new NoOpPackageActions(), _httpFactory, allowUseOfRateLimitedRegistryAPIs:false);
        
        DateTime? dateTime = await _projectManager.GetPublishedAtUtcAsync(purl, useCache: false);

        dateTime.Should().BeNull();
    }

    [Theory]
    [InlineData("pkg:cargo/rand@0.8.5")]
    public async Task PackageVersionExistsStaticEndpointAsyncSucceeds(string purlString)
    {  
        var mockHttp = new MockHttpMessageHandler();
        PackageURL purl = new(purlString);
        string packageName = purl.Name;
        string expectedStaticEndpoint = $"https://static.crates.io/rss/crates/{packageName}.xml";
        bool staticEndpointCalled = false;

        mockHttp
            .When(HttpMethod.Get, expectedStaticEndpoint)
            .Respond(req => {
                staticEndpointCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Resources.cargo_rss_rand_xml)
                };
            });

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());

        var _projectManager = new CargoProjectManager(".", new NoOpPackageActions(), mockFactory.Object);

        bool result = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

        result.Should().BeTrue();

        staticEndpointCalled.Should().BeTrue("The static endpoint should have been called.");
    }


    [Theory]
    [InlineData("pkg:cargo/rand@0.7.3")]
    public async Task PackageVersionExistsAsyncSucceeds(string purlString)
    {
        PackageURL purl = new(purlString);

        bool result = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("pkg:cargo/rand@0.7.4")]
    public async Task PackageVersionDoesntExistsAsyncSucceeds(string purlString)
    {
        PackageURL purl = new(purlString);

        bool result = await _projectManager.PackageVersionExistsAsync(purl, useCache: false);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("pkg:cargo/rand@0.7.3")] // Normal package
    public async Task MetadataSucceeds(string purlString)
    {
        PackageURL purl = new(purlString);

        PackageMetadata? metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

        metadata.Should().NotBeNull();
        purl.GetFullName().Should().Be(metadata.Name);
        purl.Version.Should().Be(metadata.PackageVersion);
        metadata.UploadTime.Should().NotBeNull();
    }

    [Theory]
    [InlineData("pkg:cargo/rand@0.7.4")] // Does Not Exist package
    public async Task MetadataFails(string purlString)
    {
        PackageURL purl = new(purlString);

        PackageMetadata? metadata = await _projectManager.GetPackageMetadataAsync(purl, useCache: false);

        metadata.Should().BeNull();
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
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
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
