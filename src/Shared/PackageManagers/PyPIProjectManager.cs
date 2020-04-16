// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    class PyPIProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_PYPI_ENDPOINT = "https://pypi.org";

        /// <summary>
        /// Download one PyPI package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>the path or file written.</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract = true)
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
                var doc = await GetJsonCache($"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json");

                if (!doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    return downloadedPaths;
                }

                foreach (var versionObject in releases.EnumerateObject())
                {
                    if (versionObject.Name != packageVersion)
                    {
                        continue;
                    }
                    foreach (var release in versionObject.Value.EnumerateArray())
                    {
                        if (!release.TryGetProperty("packagetype", out JsonElement packageType))
                        {
                            continue;   // Missing a package type
                        }
                        var result = await WebClient.GetAsync(release.GetProperty("url").GetString());
                        result.EnsureSuccessStatusCode();
                        var targetName = $"pypi-{packageType}-{packageName}@{packageVersion}";
                        if (doExtract)
                        {
                            downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync()));
                        }
                        else
                        {
                            await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                            downloadedPaths.Add(targetName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error downloading PyPI package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            try
            {
                var packageName = purl.Name;
                var doc = await GetJsonCache($"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json");
                var versionList = new List<string>();
                if (doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    foreach (var versionObject in releases.EnumerateObject())
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, versionObject.Name);
                        versionList.Add(versionObject.Name);
                    }
                }

                // Add the current version (not included in releases)
                if (doc.RootElement.TryGetProperty("info", out JsonElement info) &&
                    info.TryGetProperty("version", out JsonElement version))
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, version.GetString());
                    versionList.Add(version.GetString());
                }

                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error enumerating PyPI packages: {0}", ex.Message);
                return Array.Empty<string>();
            }
        }


        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                return await GetHttpStringCache($"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/json");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error fetching PyPI metadata: {0}", ex.Message);
                return null;
            }
        }

        protected async override Task<Dictionary<PackageURL, float>> PackageMetadataSearch(PackageURL purl, string metadata)
        {
            var mapping = new Dictionary<PackageURL, float>();
            if (purl.Name.StartsWith('_')) // TODO: there are internal modules which do not start with _
            {
                // url = 'https://github.com/python/cpython/tree/master/Lib/' + package.name,
                // TODO: it could also be in https://github.com/python/cpython/tree/master/Modules/
                mapping.Add(new PackageURL(purl.Type, purl.Namespace, purl.Name, null, null, "cpython/tree/master/Lib/"), 1.0F);
                return mapping;

            }
            if (string.IsNullOrEmpty(metadata))
            {
                return null;
            }
            JsonDocument contentJSON = JsonDocument.Parse(metadata);

            List<string> possibleProperties = new List<string>() { "homepage", "home_page" };
            JsonElement infoJSON;
            try
            {
                infoJSON = contentJSON.RootElement.GetProperty("info");
            }
            catch (Exception)
            {
                return mapping;
            }

            foreach (var property in infoJSON.EnumerateObject())
            {   // there are a couple of possibilities where the repository url might be present - check all of them
                try
                {
                    if (possibleProperties.Contains(property.Name.ToLower()))
                    {
                        string homepage = property.Value.ToString();
                        var packageUrls = GitHubProjectManager.ExtractGitHubPackageURLs(homepage);
                        // if we were able to extract a github url, return
                        if (packageUrls != null && packageUrls.Count() > 0)
                        {
                            mapping.Add(packageUrls.FirstOrDefault(), 1.0F);
                            return mapping;
                        }
                    }
                }
                catch (Exception) { continue; /* try the next property */ }
            }

            return mapping;
        }
    }
}
