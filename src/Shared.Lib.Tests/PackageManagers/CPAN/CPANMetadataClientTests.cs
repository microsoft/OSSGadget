namespace Microsoft.CST.OpenSource.Tests.PackageManagers.CPAN;

using AutoFixture;
using FluentAssertions;
using Microsoft.CST.OpenSource.PackageManagers.CPAN;
using Microsoft.CST.OpenSource.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System.Net.Http;
using System.Threading.Tasks;

public class CPANMetadataClientTests
{
    private readonly Fixture _autoFixture = new();

    [Fact]
    public async Task When_api_returns_404_then_result_status_is_404()
    {
        string packageName = _autoFixture.Create<string>();
        string expectedUrl = $"/v1/release/_search?q=distribution:{packageName}&fields=name,version&size=100";
        Dictionary<string, HttpResponseMessage> httpResponseMap = new();
        FakeHttpMessageHandler httpMessageHandler = new(httpResponseMap);
        IHttpClientFactory httpClientFactory = SetupHttpClientFactory(httpMessageHandler);
        CPANMetadataClient metadataClient = new(httpClientFactory);

        VersionsResult versionsResult = await metadataClient.GetPackageVersions(packageName, CancellationToken.None);

        httpMessageHandler.PathsRequested.ContainsKey(expectedUrl).Should().BeTrue();
        httpMessageHandler.PathsRequested[expectedUrl].Should().Be(1);
        versionsResult.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        versionsResult.ResponseContent.Should().Be(string.Empty);
        versionsResult.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task When_api_returns_two_versions_then_result_has_two_expected_versions()
    {
        string packageName = _autoFixture.Create<string>();
        string expectedUrl = $"/v1/release/_search?q=distribution:{packageName}&fields=name,version&size=100";
        string apiResponseJson = @"{
   ""_shards"" : {
      ""failed"" : 0,
      ""total"" : 3,
      ""successful"" : 3
   },
   ""took"" : 3,
   ""hits"" : {
      ""total"" : 4,
      ""hits"" : [
         {
            ""_id"" : ""fUw34Wazh12lcRrXHFoG8ZNW7Rs"",
            ""_score"" : 14.512648,
            ""_index"" : ""release_01"",
            ""fields"" : {
               ""version"" : ""v0.0.1"",
               ""name"" : ""Data-Rand-v0.0.1""
            },
            ""_type"" : ""release""
         },
         {
            ""_score"" : 14.512648,
            ""_index"" : ""release_01"",
            ""fields"" : {
               ""version"" : ""0.0.4"",
               ""name"" : ""Data-Rand-0.0.4""
            },
            ""_type"" : ""release"",
            ""_id"" : ""f977gt_5D0ET48zZWEhBcFwhiyY""
         },
      ],
      ""max_score"" : 14.512648
   },
   ""timed_out"" : false
}";
        HttpResponseMessage apiResponse = new()
        {
            Content = new StringContent(apiResponseJson),
            StatusCode = System.Net.HttpStatusCode.OK
        };
        Dictionary<string, HttpResponseMessage> httpResponseMap = new();
        httpResponseMap.Add(expectedUrl, apiResponse);
        FakeHttpMessageHandler httpMessageHandler = new(httpResponseMap);
        IHttpClientFactory httpClientFactory = SetupHttpClientFactory(httpMessageHandler);
        CPANMetadataClient metadataClient = new(httpClientFactory);

        VersionsResult versionsResult = await metadataClient.GetPackageVersions(packageName, CancellationToken.None);

        httpMessageHandler.PathsRequested.ContainsKey(expectedUrl).Should().BeTrue();
        httpMessageHandler.PathsRequested[expectedUrl].Should().Be(1);
        versionsResult.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        versionsResult.ResponseContent.Should().Be(apiResponseJson);
        versionsResult.Versions.Count().Should().Be(2);
        versionsResult.Versions[0].Should().Be("v0.0.1");
        versionsResult.Versions[1].Should().Be("0.0.4");
    }

    private static IHttpClientFactory SetupHttpClientFactory(FakeHttpMessageHandler httpMessageHandler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(
            x => new HttpClient(httpMessageHandler, false)
            );
        return httpClientFactory;
    }
}
