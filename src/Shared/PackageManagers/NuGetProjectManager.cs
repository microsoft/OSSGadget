// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using HtmlAgilityPack;
using Microsoft.CST.OpenSource.Model.Mutators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.CST.OpenSource.Shared
{
    public class NuGetProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_NUGET_ENDPOINT_API = "https://api.nuget.org";
        
        private readonly string NUGET_DEFAULT_REGISTRATION_ENDPOINT = "https://api.nuget.org/v3/registration5-gz-semver2/";
        
        private string? RegistrationEndpoint { get; set; } = null;

        public NuGetProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
            GetRegistrationEndpointAsync().Wait();
        }


        public override IList<BaseMutator> Mutators { get; } = new List<BaseMutator>()
        {
            new AfterSeparatorMutator(),
            new AsciiHomoglyphMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new SeparatorMutator(),
            new SubstitutionMutator(),
            new SuffixMutator(SuffixOverride),
            new SwapOrderOfLettersMutator(),
            new VowelSwapMutator(),
        };
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
                var doc = await GetJsonCache($"{ENV_NUGET_ENDPOINT_API}/v3/index.json");
                var resources = doc.RootElement.GetProperty("resources").EnumerateArray();
                foreach (var resource in resources)
                {
                    try
                    {
                        var _type = resource.GetProperty("@type").GetString();
                        if (_type != null && _type.Equals("RegistrationsBaseUrl/Versioned", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var _id = resource.GetProperty("@id").GetString();
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
            catch(Exception ex)
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

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                var doc = await GetJsonCache($"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json");
                var versionList = new List<string>();
                foreach (var catalogPage in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (catalogPage.TryGetProperty("items", out JsonElement itemElement))
                    {
                        foreach (var item in itemElement.EnumerateArray())
                        {
                            var catalogEntry = item.GetProperty("catalogEntry");
                            var version = catalogEntry.GetProperty("version").GetString();
                            if (version != null && version.Equals(packageVersion, StringComparison.InvariantCultureIgnoreCase))
                            {
                                var archive = catalogEntry.GetProperty("packageContent").GetString();
                                var result = await WebClient.GetAsync(archive);
                                result.EnsureSuccessStatusCode();
                                Logger.Debug("Downloading {0}...", purl?.ToString());

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
                                return downloadedPaths;
                            }
                        }
                    }
                    else
                    {
                        var subDocUrl = catalogPage.GetProperty("@id").GetString();
                        if (subDocUrl != null)
                        {
                            var subDoc = await GetJsonCache(subDocUrl);
                            foreach (var subCatalogPage in subDoc.RootElement.GetProperty("items").EnumerateArray())
                            {
                                var catalogEntry = subCatalogPage.GetProperty("catalogEntry");
                                var version = catalogEntry.GetProperty("version").GetString();
                                Logger.Debug("Identified {0} version {1}.", packageName, version);
                                if (version != null && version.Equals(packageVersion, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    var archive = catalogEntry.GetProperty("packageContent").GetString();
                                    var result = await WebClient.GetAsync(archive);
                                    result.EnsureSuccessStatusCode();
                                    Logger.Debug("Downloading {0}...", purl?.ToString());

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
                if (packageName == null)
                {
                    return new List<string>();
                }

                var doc = await GetJsonCache($"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json");
                var versionList = new List<string>();
                foreach (var catalogPage in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (catalogPage.TryGetProperty("items", out JsonElement itemElement))
                    {
                        foreach (var item in itemElement.EnumerateArray())
                        {
                            var catalogEntry = item.GetProperty("catalogEntry");
                            var version = catalogEntry.GetProperty("version").GetString();
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
                        var subDocUrl = catalogPage.GetProperty("@id").GetString();
                        if (subDocUrl != null)
                        {
                            var subDoc = await GetJsonCache(subDocUrl);
                            foreach (var subCatalogPage in subDoc.RootElement.GetProperty("items").EnumerateArray())
                            {
                                var catalogEntry = subCatalogPage.GetProperty("catalogEntry");
                                var version = catalogEntry.GetProperty("version").GetString();
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
                var packageName = purl.Name;
                if (packageName == null)
                {
                    return null;
                }
                var content = await GetHttpStringCache($"{RegistrationEndpoint}{packageName.ToLowerInvariant()}/index.json");
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

        protected async override Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
        {
            Dictionary<PackageURL, double> mapping = new Dictionary<PackageURL, double>();
            try
            {
                var packageName = purl.Name;

                // nuget doesnt provide repository information in the json metadata; we have to extract it
                // from the html home page
                HtmlWeb web = new HtmlWeb();
                HtmlDocument doc = web.Load($"{ENV_NUGET_HOMEPAGE}/{packageName}");

                var paths = new List<string>()
                {
                    "//a[@title=\"View the source code for this package\"]/@href",
                    "//a[@title=\"Visit the project site to learn more about this package\"]/@href"
                };

                foreach (string path in paths)
                {
                    string? repoCandidate = doc.DocumentNode.SelectSingleNode(path)?.GetAttributeValue("href", string.Empty);
                    if (!string.IsNullOrEmpty(repoCandidate))
                    {
                        var candidate = ExtractGitHubPackageURLs(repoCandidate).FirstOrDefault();
                        if (candidate != null)
                        {
                            mapping[candidate as PackageURL] = 1.0F;
                        }
                    }
                }
                return mapping;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching/parsing NuGet homepage: {ex.Message}");
            }

            // if nothing worked, return empty
            return mapping;
        }

        private static IEnumerable<(string Name, string Reason)> SuffixOverride(string name, string mutator)
        {
            var suffixes = new[] { "net", ".net", "nuget"};
            return suffixes.Select(s => (string.Concat(name, s), mutator + "_NUGET"));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        private static string ENV_NUGET_HOMEPAGE = "https://www.nuget.org/packages";
    }
}