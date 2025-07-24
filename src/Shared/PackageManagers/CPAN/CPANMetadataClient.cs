// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers.CPAN;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// todo: consider moving VersionsResult to a better namespace if re-used for other repositories
public class VersionsResult
{
    public bool PackageFound { get; set; }
    public List<string> Versions { get; set; } = new List<string>();
    public HttpStatusCode StatusCode { get; set; }
    public string ResponseContent { get; set; } = string.Empty;
}

public interface ICPANMetadataClient
{
    Task<VersionsResult> GetPackageVersions(string packageName, CancellationToken cancellationToken);
}

public class CPANMetadataClient : ICPANMetadataClient
{
    // metacpan-api docs: https://github.com/metacpan/metacpan-api/blob/master/docs/API-docs.md

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri _metadataUri = new Uri("https://fastapi.metacpan.org/v1/");

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        AllowTrailingCommas = true
    };

    protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public CPANMetadataClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;

        // TODO: consider making _metadataUri configurable via IConfiguration ala:
        //string? metadataEndpoint = config["cpan.urls.metadata"];
        //if(!string.IsNullOrEmpty(metadataEndpoint))
        //{
        //    _metadataUri = new Uri(metadataEndpoint);
        //}
    }

    public async Task<VersionsResult> GetPackageVersions(string packageName, CancellationToken cancellationToken)
    {
        VersionsResult result = new();

        HttpClient httpClient = _httpClientFactory.CreateClient(GetType().Name);
        httpClient.BaseAddress = _metadataUri;

        string urlEncodedName = WebUtility.UrlEncode(packageName);
        string urlPath = $"release/_search?q=distribution:{urlEncodedName}&fields=name,version&size=100";

        HttpResponseMessage httpResponse = await httpClient.GetAsync(urlPath, cancellationToken);
        result.StatusCode = httpResponse.StatusCode;

        if(httpResponse.IsSuccessStatusCode)
        {
            string json = await httpResponse.Content.ReadAsStringAsync();
            result.ResponseContent = json;

            CPANVersionResponse? versionResponse = JsonSerializer.Deserialize<CPANVersionResponse>(json, _jsonSerializerOptions);
            if(versionResponse != null)
            {
                AddResponseVersionsToResult(result, versionResponse);
            }
            else
            {
                Logger.Warn("Failed to deserialize response from CPAN metadata API");
            }
        }

        return result;
    }

    private static void AddResponseVersionsToResult(VersionsResult result, CPANVersionResponse versionResponse)
    {
        if(versionResponse.Hits.Total > 0)
        {
            result.PackageFound = true;

            foreach (CPANVersionHit hit in versionResponse.Hits.Hits)
            {
                result.Versions.Add(hit.Fields.Version);
            }
        }
    }
}
