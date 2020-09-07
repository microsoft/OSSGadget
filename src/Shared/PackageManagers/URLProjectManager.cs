// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            var url = purl.Qualifiers?.GetValueOrDefault("url") ?? "https://missing-url.com";
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
            
            var downloadedPaths = new List<string>();

            var url = purl?.Qualifiers?.GetValueOrDefault("url", null);
            if (url == null)
            {
                Logger.Error("URL not found, {0}", purl);
                return downloadedPaths;
            }
            Uri uri = new Uri(url);
            Logger.Debug("Downloading {0} ({1})...", purl, uri);
            var result = await WebClient.GetAsync(uri);
            result.EnsureSuccessStatusCode();

            var targetName = Path.GetFileName(uri.LocalPath);
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

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
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

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            return null;
        }
    }
}