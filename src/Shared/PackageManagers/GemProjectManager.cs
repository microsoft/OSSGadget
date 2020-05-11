// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    class GemProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_RUBYGEMS_ENDPOINT = "https://rubygems.org";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_RUBYGEMS_ENDPOINT_API = "https://api.rubygems.org";

        /// <summary>
        /// Download one RubyGems package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract = true, bool skipIfCached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                var url = $"{ENV_RUBYGEMS_ENDPOINT}/downloads/{packageName}-{packageVersion}.gem";
                var result = await WebClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                var targetName = $"rubygems-{packageName}@{packageVersion}";
                var extractionPath = GetDirSafePackageName(targetName);
                if (doExtract && Directory.Exists(extractionPath) && skipIfCached == true)
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }
                if (doExtract)
                {
                    downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync()));
                }
                else
                {
                    targetName += Path.GetExtension(url) ?? "";
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(targetName);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error downloading RubyGems package: {0}", ex.Message);
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

            try
            {
                var packageName = purl.Name;
                var doc = await GetJsonCache($"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/versions/{packageName}.json");
                var versionList = new List<string>();
                foreach (var gemObject in doc.RootElement.EnumerateArray())
                {
                    if (gemObject.TryGetProperty("number", out JsonElement version))
                    {
                        var vString = version.ToString();
                        // RubyGems is mostly-semver-compliant
                        vString = Regex.Replace(vString, @"(\d)pre", @"$1-pre");
                        Logger.Debug("Identified {0} version {1}.", packageName, vString);
                        versionList.Add(vString);
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error enumerating RubyGems package: {0}", ex.Message);
                return Array.Empty<string>();
            }
        }
        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_RUBYGEMS_ENDPOINT_API}/api/v1/versions/{packageName}.json");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error fetching RubyGems metadata: {0}", ex.Message);
                return null;
            }
        }
    }
}
