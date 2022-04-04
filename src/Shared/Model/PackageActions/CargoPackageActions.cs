// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.PackageActions;

using Contracts;
using Extensions;
using Helpers;
using Metadata;
using PackageManagers;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <inheritdoc/>
public class CargoPackageActions : IManagerPackageActions<CargoPackageVersionMetadata>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
    public static string ENV_CARGO_ENDPOINT = "https://crates.io";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
    public static string ENV_CARGO_ENDPOINT_STATIC = "https://static.crates.io";

    private readonly IHttpClientFactory _factory;
    
    public CargoPackageActions(IHttpClientFactory? factory = null)
    {
        _factory = factory ?? new DefaultHttpClientFactory();
    }
    
    protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <inheritdoc/>
    public async Task<string?> DownloadAsync(PackageURL packageUrl, string topLevelDirectory, string targetDirectory,
        bool doExtract,
        bool cached = false, CancellationToken cancellationToken = default)
    {
        string? packageName = packageUrl?.Name;
        string? packageVersion = packageUrl?.Version;
        string? fileName = packageUrl?.ToStringFilename();
        List<string> downloadedPaths = new();

        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion) || string.IsNullOrWhiteSpace(fileName))
        {
            Logger.Debug("Error with 'packageUrl' argument. Unable to download [{0} {1}] @ {2}. Both must be defined.", packageName, packageVersion, fileName);
            return null;
        }

        string url = $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}/{packageVersion}/download";
        try
        {
            string targetName = $"cargo-{fileName}";
            string extractionPath = Path.Combine(topLevelDirectory, targetName);
            // if the cache is already present, no need to extract
            if (doExtract && cached && Directory.Exists(extractionPath))
            {
                return extractionPath;
            }
            Logger.Debug("Downloading {0}", url);

            HttpClient httpClient = _factory.CreateClient(GetType().Name);

            HttpResponseMessage result = await httpClient.GetAsync(url, cancellationToken);
            result.EnsureSuccessStatusCode();

            if (doExtract)
            {
                return await ArchiveHelper.ExtractArchiveAsync(topLevelDirectory, targetName, await result.Content.ReadAsStreamAsync(cancellationToken), cached);
            }
            else
            {
                extractionPath += Path.GetExtension(url) ?? "";
                using FileStream outFile = File.OpenWrite(extractionPath);
                await result.Content.CopyToAsync(outFile, cancellationToken);
                return extractionPath;
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error downloading Cargo package: {0}", ex.Message);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> DoesPackageExistAsync(PackageURL packageUrl, bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(packageUrl?.Name))
        {
            Logger.Trace("Provided PackageURL was null.");
            return false;
        }
        string packageName = packageUrl.Name;
        HttpClient httpClient = _factory.CreateClient(GetType().Name);
        // Since the HTTP based calls are now moved out of BaseProjectManager these static methods should be moved somewhere else too.
        // I propose having a CacheHelper to keep the interface so we can improve the caching behavior if needed.
        return await BaseProjectManager.CheckJsonCacheForPackage(httpClient, $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}", useCache);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl, bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageUrl);

        try
        {
            string? packageName = packageUrl.Name;
            HttpClient httpClient = _factory.CreateClient(GetType().Name);
            JsonDocument doc = await BaseProjectManager.GetJsonCache(httpClient, $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}");
            List<string> versionList = new();
            foreach (JsonElement versionObject in doc.RootElement.GetProperty("versions").EnumerateArray())
            {
                if (versionObject.TryGetProperty("num", out JsonElement version))
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, version.ToString());
                    if (version.ToString() is string s)
                    {
                        versionList.Add(s);
                    }
                }
            }
            return BaseProjectManager.SortVersions(versionList.Distinct());
        }
        catch (Exception ex)
        {
            Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<string?> GetLatestVersionAsync(PackageURL packageUrl, bool includePrerelease = false,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        return (await GetAllVersionsAsync(packageUrl, useCache, cancellationToken)).FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<CargoPackageVersionMetadata?> GetMetadataAsync(PackageURL packageUrl, bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string? packageName = packageUrl.Name;
            HttpClient httpClient = _factory.CreateClient(GetType().Name);
            string? content = await BaseProjectManager.GetHttpStringCache(httpClient, $"{ENV_CARGO_ENDPOINT}/api/v1/crates/{packageName}", useCache);
            // TODO: Parse the metadata
            return new();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error fetching Cargo metadata: {0}", ex.Message);
            throw;
        }
    }
}