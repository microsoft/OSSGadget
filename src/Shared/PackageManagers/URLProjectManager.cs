// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Helpers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class URLProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "url";

        public override string ManagerType => Type;

        public URLProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public URLProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? url = purl.Qualifiers?.GetValueOrDefault("url") ?? "https://missing-url.com";
            return new Uri(url);
        }

        /// <summary>
        ///     Download one Cocoapods package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            List<string> downloadedPaths = new();
            string? url = null;
            bool foundValue = purl?.Qualifiers?.TryGetValue("url", out url) ?? false;
            if (foundValue && url is not null)
            {
                Uri uri = new(url);
                Logger.Debug("Downloading {0} ({1})...", purl, uri);
                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage? result = await httpClient.GetAsync(uri);
                result.EnsureSuccessStatusCode();

                string targetName = Path.GetFileName(uri.LocalPath);
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                if (doExtract && Directory.Exists(extractionPath) && cached == true)
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }
                if (doExtract)
                {
                    downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsStreamAsync(), cached));
                }
                else
                {
                    await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(extractionPath);
                }
                return downloadedPaths;
            }
            else
            {
                Logger.Debug("URL not found, {0}", purl);
                return downloadedPaths;
            }
        }

        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return await Task.FromResult(Array.Empty<string>());
            }

            return await Task.FromResult(new List<string>() {
                "1.0"
            });
        }

        /// <inheritdoc />
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return null;
        }
    }
}