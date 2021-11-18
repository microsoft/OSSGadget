// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class URLProjectManager : BaseProjectManager
    {
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
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            List<string> downloadedPaths = new();
            string? url = null;
            bool foundValue = purl?.Qualifiers?.TryGetValue("url", out url) ?? false;
            if (foundValue && url is not null)
            {
                Uri uri = new(url);
                Logger.Debug("Downloading {0} ({1})...", purl, uri);
                System.Net.Http.HttpResponseMessage? result = await WebClient.GetAsync(uri);
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
                    downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync(), cached));
                }
                else
                {
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(targetName);
                }
                return downloadedPaths;
            }
            else
            {
                Logger.Debug("URL not found, {0}", purl);
                return downloadedPaths;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }

            return new List<string>() {
                "1.0"
            };
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<string?> GetMetadata(PackageURL purl)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return null;
        }
    }
}