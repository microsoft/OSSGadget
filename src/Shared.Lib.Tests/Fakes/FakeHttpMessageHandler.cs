// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;


// manual fake class necessary because NSubstitute can't mock the internal protected Send/SendAsync methods of HttpMessageHandler
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, HttpResponseMessage> _responseMap;
    public FakeHttpMessageHandler(Dictionary<string, HttpResponseMessage> responseMap)
    {
        _responseMap = responseMap;
    }

    public Dictionary<string, int> PathsRequested { get; private set; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = Send(request, cancellationToken);
        return Task.FromResult(response);
    }

    private readonly HttpResponseMessage _notFoundResponse = new()
    {
        StatusCode = HttpStatusCode.NotFound
    };

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? url = request.RequestUri?.PathAndQuery;
        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("Invalid test setup. Expected HttpRequestMessage.RequestUri to be non-null.");
        }

        if (PathsRequested.TryGetValue(url, out _))
        {
            PathsRequested[url]++;
        }
        else
        {
            PathsRequested.Add(url, 1);
        }

        if (_responseMap.TryGetValue(url, out HttpResponseMessage? response) && response != null)
        {
            return response;
        }

        return _notFoundResponse;
    }
}
