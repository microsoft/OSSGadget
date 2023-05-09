// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model;
using PackageUrl;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

public interface ITypedManager<TArtifactUriType> : IBaseProjectManager where TArtifactUriType : Enum
{
    /// <summary>
    /// Gets the relevant URI(s) to download the files related to a <see cref="PackageURL"/>.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to get the URI(s) for.</param>
    /// <returns>A list of the relevant <see cref="ArtifactUri{TArtifactUriType}"/>.</returns>
    /// <remarks>Returns the expected URIs for resources. Does not validate that the URIs resolve at the moment of enumeration.</remarks>
    [Obsolete(message: $"Deprecated in favor of {nameof(GetArtifactDownloadUrisAsync)}.")]
    IEnumerable<ArtifactUri<TArtifactUriType>> GetArtifactDownloadUris(PackageURL purl);

    /// <summary>
    /// Gets the relevant URI(s) to download the files related to a <see cref="PackageURL"/>.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to get the URI(s) for.</param>
    /// <param name="useCache">If the data should be retrieved from the cache. Defaults to <c>true</c>.</param>
    /// <returns>A list of the relevant <see cref="ArtifactUri{TArtifactUriType}"/>.</returns>
    /// <remarks>Returns the expected URIs for resources. Does not validate that the URIs resolve at the moment of enumeration.</remarks>
    IAsyncEnumerable<ArtifactUri<TArtifactUriType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true);

    /// <summary>
    /// Gets all <see cref="PackageURL"/>s associated with an owner.
    /// </summary>
    /// <param name="owner">The username of the owner.</param>
    /// <param name="useCache">If the data should be retrieved from the cache. Defaults to <c>true</c>.</param>
    /// <returns>A list of the <see cref="PackageURL"/>s from this owner.</returns>
    IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true);

    /// <summary>
    /// Check to see if the <see cref="Uri"/> exists.
    /// </summary>
    /// <param name="artifactUri">The <see cref="Uri"/> to check if exists.</param>
    /// <param name="policy">An optional <see cref="AsyncRetryPolicy"/> to use with the http request.</param>
    /// <returns>If the request returns <see cref="HttpStatusCode.OK"/>.</returns>
    Task<bool> UriExistsAsync(Uri artifactUri, AsyncRetryPolicy<HttpResponseMessage>? policy = null);
}