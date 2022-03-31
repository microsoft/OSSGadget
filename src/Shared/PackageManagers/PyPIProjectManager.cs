// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Model;
    using NLog.LayoutRenderers.Wrappers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Utilities;
    using Version = SemanticVersioning.Version;

    public class PyPIProjectManager : BaseProjectManager
    {
        public static string ENV_PYPI_ENDPOINT { get; set; } = "https://pypi.org";

        public PyPIProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public PyPIProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        /// Download one PyPI package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>the path or file written.</returns>
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

                JsonDocument? doc = await GetJsonCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json");

                if (!doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    return downloadedPaths;
                }

                foreach (JsonProperty versionObject in releases.EnumerateObject())
                {
                    if (versionObject.Name != packageVersion)
                    {
                        continue;
                    }
                    foreach (JsonElement release in versionObject.Value.EnumerateArray())
                    {
                        if (!release.TryGetProperty("packagetype", out JsonElement packageType))
                        {
                            continue;   // Missing a package type
                        }

                        System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(release.GetProperty("url").GetString());
                        result.EnsureSuccessStatusCode();
                        string targetName = $"pypi-{packageType}-{packageName}@{packageVersion}";
                        string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                        if (doExtract && Directory.Exists(extractionPath) && cached == true)
                        {
                            downloadedPaths.Add(extractionPath);
                            return downloadedPaths;
                        }
                        if (doExtract)
                        {
                            downloadedPaths.Add(await ExtractArchive(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsByteArrayAsync(), cached));
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
                Logger.Debug(ex, "Error downloading PyPI package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
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

            return await CheckJsonCacheForPackage(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json", useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();

                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{packageName}/json", useCache);
                List<string> versionList = new();
                if (doc.RootElement.TryGetProperty("releases", out JsonElement releases))
                {
                    foreach (JsonProperty versionObject in releases.EnumerateObject())
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
                    if (version.GetString() is string versionString && !string.IsNullOrWhiteSpace(versionString))
                    {
                        versionList.Add(versionString);
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

        public override async Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
        {
            try
            {
                HttpClient httpClient = CreateHttpClient();

                return await GetHttpStringCache(httpClient, $"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/json", useCache);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching PyPI metadata: {0}", ex.Message);
                return null;
            }
        }

        /// <inheritdoc />
        public override async Task<PackageMetadata> GetPackageMetadata(PackageURL purl, bool useCache = true)
        {
            PackageMetadata metadata = new();
            string? content = await GetMetadata(purl, useCache);
            if (string.IsNullOrEmpty(content)) { return metadata; }

            // convert NPM package data to normalized form
            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            JsonElement infoElement = root.GetProperty("info");

            metadata.Name = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "name");
            metadata.Description = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "description");
            string? summary = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "summary");
            if (string.IsNullOrWhiteSpace(metadata.Description))
            { // longer string might be the actual description
                metadata.Description = summary;
            }
            metadata.PackageManagerUri = ENV_PYPI_ENDPOINT;
            metadata.PackageUri = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "package_url");
            metadata.Keywords = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(infoElement, "keywords"));

            // author
            User author = new()
            {
                Name = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "author"),
                Email = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "author_email"),
            };
            metadata.Authors ??= new List<Model.User>();
            metadata.Authors.Add(author);

            // maintainers
            User maintainer = new()
            {
                Name = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "maintainer"),
                Email = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "maintainer_email"),
            };
            metadata.Maintainers ??= new List<User>();
            metadata.Maintainers.Add(maintainer);

            // repository
            Dictionary<PackageURL, double>? repoMappings = await SearchRepoUrlsInPackageMetadata(purl, content);
            foreach (KeyValuePair<PackageURL, double> repoMapping in repoMappings)
            {
                Repository repository = new()
                {
                    Rank = repoMapping.Value,
                    Type = repoMapping.Key.Type
                };
                await repository.ExtractRepositoryMetadata(repoMapping.Key);

                metadata.Repository ??= new List<Repository>();
                metadata.Repository.Add(repository);
            }

            // license
            string? licenseType = OssUtilities.GetJSONPropertyStringIfExists(infoElement, "license");
            if (!string.IsNullOrWhiteSpace(licenseType))
            {
                metadata.Licenses ??= new List<License>();
                metadata.Licenses.Add(new License()
                {
                    Name = licenseType
                });
            }

            // get the version
            List<Version> versions = GetVersions(contentJSON);
            Version? latestVersion = GetLatestVersion(versions);

            if (purl.Version != null)
            {
                // find the version object from the collection
                metadata.PackageVersion = purl.Version;
            }
            else
            {
                metadata.PackageVersion = latestVersion is null ? purl.Version : latestVersion?.ToString();
            }

            // if we found any version at all, get the deets
            if (metadata.PackageVersion is not null)
            {
                Version versionToGet = new(metadata.PackageVersion);
                JsonElement? versionElement = GetVersionElement(contentJSON, versionToGet);
                if (versionElement is not null)
                {
                    // fill the version specific entries

                    // digests
                    if (OssUtilities.GetJSONPropertyIfExists(versionElement, "digests")?.EnumerateObject()
                        is JsonElement.ObjectEnumerator digests)
                    {
                        metadata.Signature ??= new List<Digest>();
                        foreach (JsonProperty digest in digests)
                        {
                            metadata.Signature.Add(new Digest()
                            {
                                Algorithm = digest.Name,
                                Signature = digest.Value.ToString()
                            });
                        }
                    }

                    // downloads
                    if (OssUtilities.GetJSONPropertyIfExists(versionElement, "downloads")?.GetInt64() is long downloads
                        && downloads != -1)
                    {
                        metadata.Downloads ??= new Downloads()
                        {
                            Overall = downloads
                        };
                    }

                    metadata.Size = OssUtilities.GetJSONPropertyIfExists(versionElement, "size")?.GetInt64();
                    metadata.UploadTime = OssUtilities.GetJSONPropertyStringIfExists(versionElement, "upload_time");
                    metadata.Active = !OssUtilities.GetJSONPropertyIfExists(versionElement, "yanked")?.GetBoolean();
                    metadata.VersionUri = $"{ENV_PYPI_ENDPOINT}/project/{purl.Name}/{purl.Version}";
                    metadata.VersionDownloadUri = OssUtilities.GetJSONPropertyStringIfExists(versionElement, "url");
                }
            }

            return metadata;
        }

        public override List<Version> GetVersions(JsonDocument? contentJSON)
        {
            List<Version> allVersions = new();
            if (contentJSON is null) { return allVersions; }

            Console.WriteLine(JsonSerializer.Serialize(contentJSON));
            JsonElement root = contentJSON.RootElement;
            try
            {
                JsonElement versions = root.GetProperty("versions");
                foreach (JsonProperty version in versions.EnumerateObject())
                {
                    allVersions.Add(new Version(version.Name));
                }
            }
            catch (KeyNotFoundException) { return allVersions; }
            catch (InvalidOperationException) { return allVersions; }

            return allVersions;
        }

        public override JsonElement? GetVersionElement(JsonDocument contentJSON, Version version)
        {
            try
            {
                JsonElement versionElement = contentJSON.RootElement.GetProperty("releases").GetProperty(version.ToString());
                return versionElement;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_PYPI_ENDPOINT}/project/{purl?.Name}");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Dictionary<PackageURL, double> mapping = new();
            if (purl.Name?.StartsWith('_') ?? false) // TODO: there are internal modules which do not start with _
            {
                // TODO: internal modules could also be in https://github.com/python/cpython/tree/master/Modules/
                mapping.Add(new PackageURL(purl.Type, purl.Namespace, purl.Name, null, null, "cpython/tree/master/Lib/"), 1.0F);
                return mapping;
            }
            if (string.IsNullOrEmpty(metadata))
            {
                return mapping;
            }
            JsonDocument contentJSON = JsonDocument.Parse(metadata);

            List<string> possibleProperties = new() { "homepage", "home_page" };
            JsonElement infoJSON;
            try
            {
                infoJSON = contentJSON.RootElement.GetProperty("info");
            }
            catch (Exception)
            {
                return mapping;
            }

            foreach (JsonProperty property in infoJSON.EnumerateObject())
            {   // there are a couple of possibilities where the repository url might be present - check all of them
                try
                {
                    if (possibleProperties.Contains(property.Name.ToLower()))
                    {
                        string homepage = property.Value.ToString() ?? string.Empty;
                        IEnumerable<PackageURL>? packageUrls = GitHubProjectManager.ExtractGitHubPackageURLs(homepage);
                        // if we were able to extract a github url, return
                        if (packageUrls != null && packageUrls.Any())
                        {
                            mapping.Add(packageUrls.First(), 1.0F);
                            return mapping;
                        }
                    }
                    else if (property.Name.Equals("project_urls"))
                    {
                        if (property.Value.TryGetProperty("Source",out JsonElement jsonElement))
                        {
                            string? sourceLoc = jsonElement.GetString();
                            if (sourceLoc != null)
                            {
                                IEnumerable<PackageURL>? packageUrls = GitHubProjectManager.ExtractGitHubPackageURLs(sourceLoc);
                                if (packageUrls != null && packageUrls.Any())
                                {
                                    mapping.Add(packageUrls.First(), 1.0F);
                                    return mapping;
                                }
                            }
                        }
                    }
                }
                catch (Exception) { continue; /* try the next property */ }
            }

            return mapping;
        }
    }
}