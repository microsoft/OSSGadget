// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Helpers;

using Extensions;
using Moq;
using PackageManagers;
using PackageUrl;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

public static class FindSquatsHelper
{
    /// <summary>
    /// Set up a mock of <see cref="IHttpClientFactory"/> for this test run.
    /// </summary>
    /// <param name="mockHttpClientFactory">The <see cref="Mock{IHttpClientFactory}"/> to construct.</param>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    /// <param name="validSquats">The list of squats to add to the <paramref name="mockHttpClientFactory"/>.</param>
    /// <returns>A Mocked <see cref="IHttpClientFactory"/>.</returns>
    public static IHttpClientFactory SetupHttpCalls(
        Mock<IHttpClientFactory>? mockHttpClientFactory = null,
        PackageURL? purl = null,
        IEnumerable<string>? validSquats = null)
    {
        mockHttpClientFactory ??= new Mock<IHttpClientFactory>();
        using MockHttpMessageHandler httpMock = new();

        if (purl is not null)
        {
            if (validSquats is not null)
            {
                // Mock the other packages that "exist" if a list of "valid" squats was provided.
                MockSquattedPackages(httpMock, validSquats);
            }

            // Mock that this package exists.
            MockHttpFetchResponse(HttpStatusCode.OK, GetRegistryUrl(purl), httpMock);
        }

        mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpMock.ToHttpClient());

        // Return the mocked IHttpClientFactory.
        return mockHttpClientFactory.Object;
    }
    
    private static void MockHttpFetchResponse(
        HttpStatusCode statusCode,
        string url,
        MockHttpMessageHandler httpMock)
    {
        httpMock
            .When(HttpMethod.Get, url)
            .Respond(statusCode, "application/json", "{}");
    }
        
    private static void MockSquattedPackages(MockHttpMessageHandler httpMock, IEnumerable<string> squattedPurls)
    {
        foreach (PackageURL mutatedPackage in squattedPurls.Select(mutatedPurl => new PackageURL(mutatedPurl)))
        {
            string url = GetRegistryUrl(mutatedPackage);
            MockHttpFetchResponse(HttpStatusCode.OK, url, httpMock);
        }
    }

    public static string GetRegistryUrl(PackageURL purl)
    {
        return purl.Type switch
        {
            "npm" => $"{NPMProjectManager.DEFAULT_NPM_API_ENDPOINT}/{purl.GetFullName()}",
            "nuget" => $"{NuGetProjectManager.NUGET_DEFAULT_REGISTRATION_ENDPOINT}{purl.Name.ToLowerInvariant()}/index.json",
            "pypi" => $"{PyPIProjectManager.DEFAULT_PYPI_ENDPOINT}/pypi/{purl.Name}/json",
            _ => throw new NotSupportedException(
                $"{purl.Type} packages are not currently supported."),
        };
    }
}