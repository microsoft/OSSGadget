// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers;

using Contracts;
using Extensions;
using Model;
using PackageUrl;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

/// <summary>
/// An abstract class that implements <see cref="BaseProjectManager"/> that defines an implementation of
/// <see cref="IManagerPackageVersionMetadata"/> to be associated with the manager that implements this class.
/// </summary>
/// <typeparam name="T">
/// The implementation of <see cref="IManagerPackageVersionMetadata"/> for the manager that implements this class.
/// </typeparam>
/// <typeparam name="TArtifactUriType">
/// The <see cref="Enum"/> for the valid types a URI of this manager could be.
/// </typeparam>
/// TODO: Combine ArtifactUriType and PackageVersionMetadata as they will always be linked. https://github.com/microsoft/OSSGadget/issues/333
public abstract class TypedManager<T, TArtifactUriType> : BaseProjectManager, ITypedManager<TArtifactUriType> where T : IManagerPackageVersionMetadata where TArtifactUriType : Enum
{
    /// <summary>
    /// The actions object to be used in the project manager.
    /// </summary>
    protected readonly IManagerPackageActions<T> Actions;

    protected TypedManager(IManagerPackageActions<T> actions, IHttpClientFactory httpClientFactory, string directory) : base(httpClientFactory, directory)
    {
        Actions = actions;
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("DownloadVersion {0}", purl.ToString());

        string fileName = purl.ToStringFilename();
        string targetName = $"{GetType().Name}-{fileName}";
        string? containingPath = await Actions.DownloadAsync(purl, TopLevelExtractionDirectory, targetName, doExtract, cached);

        if (containingPath is string notNullPath)
        {
            if (doExtract)
            {
                return Directory.EnumerateFiles(notNullPath, "*",
                    new EnumerationOptions() {RecurseSubdirectories = true});
            }

            return new[] { containingPath };
        }

        return Array.Empty<string>();
    }

    /// <inheritdoc />
    public override async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("PackageExists {0}", purl.ToString());
        return await Actions.DoesPackageExistAsync(purl, useCache);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<string>> EnumerateVersionsAsync(
        PackageURL purl,
        bool useCache = true,
        bool includePrerelease = true)
    {
        ArgumentNullException.ThrowIfNull(purl, nameof(purl));
        Logger.Trace("EnumerateVersions {0}", purl.ToString());
        return await Actions.GetAllVersionsAsync(purl, includePrerelease, useCache);
    }

    /// <inheritdoc />
    public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
    {
        return (await Actions.GetMetadataAsync(purl, useCache))?.ToString();
    }

    /// <inheritdoc />
    [Obsolete(message: $"Deprecated in favor of {nameof(GetArtifactDownloadUrisAsync)}.")]
    public IEnumerable<ArtifactUri<TArtifactUriType>> GetArtifactDownloadUris(PackageURL purl)
    {
        return GetArtifactDownloadUrisAsync(purl).ToListAsync().GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Gets the relevant URI(s) to download the files related to a <see cref="PackageURL"/>.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to get the URI(s) for.</param>
    /// <param name="useCache">If the data should be retrieved from the cache. Defaults to <c>true</c>.</param>
    /// <returns>A list of the relevant <see cref="ArtifactUri{TArtifactUriType}"/>.</returns>
    /// <remarks>Returns the expected URIs for resources. Does not validate that the URIs resolve at the moment of enumeration.</remarks>
    public abstract IAsyncEnumerable<ArtifactUri<TArtifactUriType>> GetArtifactDownloadUrisAsync(PackageURL purl, bool useCache = true);
    
    /// <summary>
    /// Gets all <see cref="PackageURL"/>s associated with an owner.
    /// </summary>
    /// <param name="owner">The username of the owner.</param>
    /// <param name="useCache">If the data should be retrieved from the cache. Defaults to <c>true</c>.</param>
    /// <returns>A list of the <see cref="PackageURL"/>s from this owner.</returns>
    public abstract IAsyncEnumerable<PackageURL> GetPackagesFromOwnerAsync(string owner, bool useCache = true);

    /// <inheritdoc />
    public async Task<bool> UriExistsAsync(Uri artifactUri, AsyncRetryPolicy<HttpResponseMessage>? policy = null)
    {
        policy ??= DefaultPolicy;

        return (await policy.ExecuteAsync(() => CreateHttpClient().GetAsync(artifactUri, HttpCompletionOption.ResponseHeadersRead))).StatusCode == HttpStatusCode.OK;
    }

    /// <summary>
    /// The delay to use in the <see cref="DefaultPolicy"/>.
    /// </summary>
    private static readonly IEnumerable<TimeSpan> Delay =
        Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(100), retryCount: 5);

    /// <summary>
    /// The default policy to use when checking to see existence of the <see cref="Uri"/>.
    /// </summary>
    private static readonly AsyncRetryPolicy<HttpResponseMessage> DefaultPolicy =
        Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => r.StatusCode != HttpStatusCode.OK) // Also consider any response that doesn't have a 200 status code to be a failure.
            .WaitAndRetryAsync(Delay);
} 