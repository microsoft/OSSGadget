// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using HtmlAgilityPack;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class NuGetProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_NUGET_ENDPOINT_API = "https://api.nuget.org";

        private readonly string NUGET_DEFAULT_REGISTRATION_ENDPOINT = "https://api.nuget.org/v3/registration5-gz-semver2/";

        private string? RegistrationEndpoint { get; set; } = null;

        public NuGetProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
            GetRegistrationEndpointAsync().Wait();
        }

        public NuGetProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
            GetRegistrationEndpointAsync().Wait();
        }

        /// <summary>
        /// Dynamically identifies the registration endpoint.
        /// </summary>
        /// <returns>NuGet registration endpoint</returns>
        private async Task<string> GetRegistrationEndpointAsync()
        {
            if (RegistrationEndpoint != null)
            {
                return RegistrationEndpoint;
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_NUGET_ENDPOINT_API}/v3/index.json");
                JsonElement.ArrayEnumerator resources = doc.RootElement.GetProperty("resources").EnumerateArray();
                foreach (JsonElement resource in resources)
                {
                    try
                    {
                        string? _type = resource.GetProperty("@type").GetString();
                        if (_type != null && _type.Equals("RegistrationsBaseUrl/Versioned", StringComparison.InvariantCultureIgnoreCase))
                        {
                            string? _id = resource.GetProperty("@id").GetString();
                            if (!string.IsNullOrWhiteSpace(_id))
                            {
                                RegistrationEndpoint = _id;
                                return _id;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex, "Error parsing NuGet API endpoint: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error parsing NuGet API endpoint: {0}", ex.Message);
            }
            RegistrationEndpoint = NUGET_DEFAULT_REGISTRATION_ENDPOINT;
            return RegistrationEndpoint;
        }

        /// <summary>
        ///     Download one NuGet package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                JsonDocument doc = await GetJsonCache(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json");
                List<string> versionList = new();
                foreach (JsonElement catalogPage in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (catalogPage.TryGetProperty("items", out JsonElement itemElement))
                    {
                        foreach (JsonElement item in itemElement.EnumerateArray())
                        {
                            JsonElement catalogEntry = item.GetProperty("catalogEntry");
                            string? version = catalogEntry.GetProperty("version").GetString();
                            if (version != null && version.Equals(packageVersion, StringComparison.InvariantCultureIgnoreCase))
                            {
                                string? archive = catalogEntry.GetProperty("packageContent").GetString();
                                HttpResponseMessage? result = await httpClient.GetAsync(archive);
                                result.EnsureSuccessStatusCode();
                                Logger.Debug("Downloading {0}...", purl?.ToString());

                                string? targetName = $"nuget-{packageName}@{packageVersion}";
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
                                return downloadedPaths;
                            }
                        }
                    }
                    else
                    {
                        string? subDocUrl = catalogPage.GetProperty("@id").GetString();
                        if (subDocUrl != null)
                        {
                            JsonDocument subDoc = await GetJsonCache(httpClient, subDocUrl);
                            foreach (JsonElement subCatalogPage in subDoc.RootElement.GetProperty("items").EnumerateArray())
                            {
                                JsonElement catalogEntry = subCatalogPage.GetProperty("catalogEntry");
                                string? version = catalogEntry.GetProperty("version").GetString();
                                Logger.Debug("Identified {0} version {1}.", packageName, version);
                                if (version != null && version.Equals(packageVersion, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    string? archive = catalogEntry.GetProperty("packageContent").GetString();
                                    HttpResponseMessage? result = await httpClient.GetAsync(archive);
                                    result.EnsureSuccessStatusCode();
                                    Logger.Debug("Downloading {0}...", purl?.ToString());

                                    string targetName = $"nuget-{packageName}@{packageVersion}";
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
                                    return downloadedPaths;
                                }
                            }
                        }
                        else
                        {
                            Logger.Debug("Catalog identifier was null.");
                        }
                    }
                }
                Logger.Debug("Unable to find NuGet package.");
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading NuGet package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <summary>
        /// Check if the package exists in the respository.
        /// </summary>
        /// <param name="purl">The PackageURL to check.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <returns>True if the package is confirmed to exist in the repository. False otherwise.</returns>
        public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            HttpClient httpClient = CreateHttpClient();

            return await CheckJsonCacheForPackage(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json", useCache);
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());

            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                string packageName = purl.Name;

                JsonDocument doc = await GetJsonCache(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json");
                List<string> versionList = new();
                foreach (JsonElement catalogPage in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (catalogPage.TryGetProperty("items", out JsonElement itemElement))
                    {
                        foreach (JsonElement item in itemElement.EnumerateArray())
                        {
                            JsonElement catalogEntry = item.GetProperty("catalogEntry");
                            string? version = catalogEntry.GetProperty("version").GetString();
                            if (version != null)
                            {
                                Logger.Debug("Identified {0} version {1}.", packageName, version);
                                versionList.Add(version);
                            }
                            else
                            {
                                Logger.Debug("Identified {0} version NULL. This might indicate a parsing error.", packageName);
                            }
                        }
                    }
                    else
                    {
                        string? subDocUrl = catalogPage.GetProperty("@id").GetString();
                        if (subDocUrl != null)
                        {
                            JsonDocument subDoc = await GetJsonCache(httpClient, subDocUrl);
                            foreach (JsonElement subCatalogPage in subDoc.RootElement.GetProperty("items").EnumerateArray())
                            {
                                JsonElement catalogEntry = subCatalogPage.GetProperty("catalogEntry");
                                string? version = catalogEntry.GetProperty("version").GetString();
                                Logger.Debug("Identified {0} version {1}.", packageName, version);
                                if (version != null)
                                {
                                    Logger.Debug("Identified {0} version {1}.", packageName, version);
                                    versionList.Add(version);
                                }
                                else
                                {
                                    Logger.Debug("Identified {0} version NULL. This might indicate a parsing error.", packageName);
                                }
                            }
                        }
                        else
                        {
                            Logger.Debug("Catalog identifier was null.");
                        }
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                string? packageName = purl?.Name;
                if (packageName == null)
                {
                    return null;
                }
                HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, $"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching NuGet metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_NUGET_HOMEPAGE}/{purl?.Name}");
        }

        protected override async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
        {
            Dictionary<PackageURL, double> mapping = new();
            try
            {
                string? packageName = purl.Name;

                // nuget doesnt provide repository information in the json metadata; we have to extract it
                // from the html home page
                HtmlWeb web = new();
                HtmlDocument doc = web.Load($"{ENV_NUGET_HOMEPAGE}/{packageName}");

                List<string> paths = new()
                {
                    "//a[@title=\"View the source code for this package\"]/@href",
                    "//a[@title=\"Visit the project site to learn more about this package\"]/@href"
                };
                await Task.Run(() =>
                {
                    foreach (string path in paths)
                    {
                        string? repoCandidate = doc.DocumentNode.SelectSingleNode(path)?.GetAttributeValue("href", string.Empty);
                        if (!string.IsNullOrEmpty(repoCandidate))
                        {
                            PackageURL? candidate = ExtractGitHubPackageURLs(repoCandidate).FirstOrDefault();
                            if (candidate != null)
                            {
                                mapping[candidate] = 1.0F;
                            }
                        }
                    }
                });
                return mapping;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching/parsing NuGet homepage: {ex.Message}");
            }

            // if nothing worked, return empty
            return mapping;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        private static string ENV_NUGET_HOMEPAGE = "https://www.nuget.org/packages";
    }
}