// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Microsoft.CST.OpenSource.Shared
{
    class NuGetProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_NUGET_ENDPOINT_API = "https://api.nuget.org";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        static string ENV_NUGET_HOMEPAGE = "https://www.nuget.org/packages";

        /// <summary>
        /// Download one NuGet package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
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
                var doc = await GetJsonCache($"{ENV_NUGET_ENDPOINT_API}/v3/registration3/{packageName}/{packageVersion}.json");
                var archive = doc.RootElement.GetProperty("packageContent").GetString();
                var result = await WebClient.GetAsync(archive);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl.ToString());

                var targetName = $"nuget-{packageName}@{packageVersion}";
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
                    targetName += Path.GetExtension(archive) ?? "";
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(targetName);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error downloading NuGet package: {0}", ex.Message);
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
                var doc = await GetJsonCache($"{ENV_NUGET_ENDPOINT_API}/v3/registration3/{packageName}/index.json");
                var versionList = new List<string>();
                foreach (var catalogPage in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    foreach (var item in catalogPage.GetProperty("items").EnumerateArray())
                    {
                        var catalogEntry = item.GetProperty("catalogEntry");
                        var version = catalogEntry.GetProperty("version").GetString();
                        Logger.Debug("Identified {0} version {1}.", packageName, version);
                        versionList.Add(version);
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating NuGet packages: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_NUGET_ENDPOINT_API}/v3/registration3/{packageName.ToLower()}/index.json");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching NuGet metadata: {ex.Message}");
                return null;
            }
        }

        protected async override Task<Dictionary<PackageURL, double>> PackageMetadataSearch(PackageURL purl, string metadata)
        {
            Dictionary<PackageURL, double> mapping = new Dictionary<PackageURL, double>();
            try
            {
                var packageName = purl.Name;

                // nuget doesnt provide repository information in the json metadata; we have to extract it from the html home page
                HtmlWeb web = new HtmlWeb();
                HtmlDocument doc = web.Load($"{ENV_NUGET_HOMEPAGE}/{packageName}");

                var paths = new List<string>()
                {
                    "//a[@title=\"View the source code for this package\"]/@href",
                    "//a[@title=\"Visit the project site to learn more about this package\"]/@href"
                };

                foreach (string path in paths)
                {
                    string repoCandidate = doc.DocumentNode.SelectSingleNode(path).GetAttributeValue("href", string.Empty);
                    if (!string.IsNullOrEmpty(repoCandidate))
                    {
                        PackageURL repoPurl = GitHubProjectManager.ExtractGitHubPackageURLs(repoCandidate).ToList().FirstOrDefault();
                        mapping.Add(repoPurl, 1.0F);
                        return mapping;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching/parsing NuGet homepage: {ex.Message}");
                return mapping;
            }

            // if nothing worked, return empty
            return mapping;
        }
    }
}
