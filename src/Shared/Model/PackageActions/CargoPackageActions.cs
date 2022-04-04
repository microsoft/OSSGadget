// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.PackageActions;

using Contracts;
using Extensions;
using Helpers;
using Metadata;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
                Logger.Debug("Error with 'purl' argument. Unable to download [{0} {1}] @ {2}. Both must be defined.", packageName, packageVersion, fileName);
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

                System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();

                if (doExtract)
                {
                    return await ArchiveHelper.ExtractArchiveAsync(topLevelDirectory, targetName, await result.Content.ReadAsStreamAsync(), cached);
                }
                else
                {
                    extractionPath += Path.GetExtension(url) ?? "";
                    using FileStream outFile = File.OpenWrite(extractionPath);
                    await result.Content.CopyToAsync(outFile);
                    return extractionPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading Cargo package: {0}", ex.Message);
            }
    }

    public Task<bool> DoesPackageExistAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    public Task<IEnumerable<string>> GetAllVersionsAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    public Task<string> GetLatestVersionAsync(PackageURL packageUrl, bool includePrerelease = false, bool useCache = true,
        CancellationToken cancellationToken = default) =>
        throw new System.NotImplementedException();

    public Task<CargoPackageVersionMetadata?> GetMetadataAsync(PackageURL packageUrl, bool useCache = true, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
}