// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Model;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using License = Microsoft.CST.OpenSource.Model.License;
using Repository = Microsoft.CST.OpenSource.Model.Repository;
using User = Microsoft.CST.OpenSource.Model.User;
using Version = SemVer.Version;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class PyPIProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_PYPI_ENDPOINT = "https://pypi.org";

        public PyPIProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one PyPI package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> the path or file written. </returns>
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
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading PyPI package: {0}", ex.Message);
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
                    if (version.GetString() is string versionString && !string.IsNullOrWhiteSpace(versionString))
                        versionList.Add(versionString);
                }

                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Warn("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                return await GetHttpStringCache($"{ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/json");
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching PyPI metadata: {0}", ex.Message);
                return null;
            }
        }

        public override async Task<PackageMetadata> GetPackageMetadata(PackageURL purl)
        {
            PackageMetadata metadata = new PackageMetadata();
            string? content = await GetMetadata(purl);
            if (string.IsNullOrEmpty(content)) { return metadata; }

            // convert NPM package data to normalized form
            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            JsonElement infoElement = root.GetProperty("info");

            metadata.Name = Utilities.GetJSONPropertyStringIfExists(infoElement, "name");
            metadata.Description = Utilities.GetJSONPropertyStringIfExists(infoElement, "description");
            string? summary = Utilities.GetJSONPropertyStringIfExists(infoElement, "summary");
            if (string.IsNullOrWhiteSpace(metadata.Description))
            { // longer string might be the actual description
                metadata.Description = summary;
            }
            metadata.PackageManagerUri = ENV_PYPI_ENDPOINT;
            metadata.Package_Uri = Utilities.GetJSONPropertyStringIfExists(infoElement, "package_url");
            metadata.Keywords = Utilities.ConvertJSONToList(Utilities.GetJSONPropertyIfExists(infoElement, "keywords"));

            // author
            User author = new User()
            {
                Name = Utilities.GetJSONPropertyStringIfExists(infoElement, "author"),
                Email = Utilities.GetJSONPropertyStringIfExists(infoElement, "author_email"),
            };
            metadata.Authors ??= new List<Model.User>();
            metadata.Authors.Add(author);

            // maintainers
            User maintainer = new User()
            {
                Name = Utilities.GetJSONPropertyStringIfExists(infoElement, "maintainer"),
                Email = Utilities.GetJSONPropertyStringIfExists(infoElement, "maintainer_email"),
            };
            metadata.Maintainers ??= new List<User>();
            metadata.Maintainers.Add(maintainer);

            // repository
            var repoMappings = await SearchRepoUrlsInPackageMetadata(purl, content);
            foreach (var repoMapping in repoMappings)
            {
                Repository repository = new Repository
                {
                    Rank = repoMapping.Value,
                    Type = repoMapping.Key.Type
                };
                await repository.ExtractRepositoryMetadata(repoMapping.Key);

                metadata.Repository ??= new List<Repository>();
                metadata.Repository.Add(repository);
            }

            // license
            var licenseType = Utilities.GetJSONPropertyStringIfExists(infoElement, "license");
            if (!string.IsNullOrWhiteSpace(licenseType))
            {
                metadata.Licenses ??= new List<License>();
                metadata.Licenses.Add(new License()
                {
                    Name = licenseType
                });
            }

            // get the version
            var versions = GetVersions(contentJSON);
            var latestVersion = GetLatestVersion(versions);

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
                Version versionToGet = new Version(metadata.PackageVersion);
                JsonElement? versionElement = GetVersionElement(contentJSON, versionToGet);
                if (versionElement is not null)
                {
                    // fill the version specific entries

                    // digests
                    if (Utilities.GetJSONPropertyIfExists(versionElement, "digests")?.EnumerateObject()
                        is JsonElement.ObjectEnumerator digests)
                    {
                        metadata.Signature ??= new List<Digest>();
                        foreach (var digest in digests)
                        {
                            metadata.Signature.Add(new Digest()
                            {
                                Algorithm = digest.Name,
                                Signature = digest.Value.ToString()
                            });
                        }
                    }

                    // downloads
                    if (Utilities.GetJSONPropertyIfExists(versionElement, "downloads")?.GetInt64() is long downloads
                        && downloads != -1)
                    {
                        metadata.Downloads ??= new Downloads()
                        {
                            Overall = downloads
                        };
                    }

                    metadata.Size = Utilities.GetJSONPropertyIfExists(versionElement, "size")?.GetInt64();
                    metadata.UploadTime = Utilities.GetJSONPropertyStringIfExists(versionElement, "upload_time");
                    metadata.Active = !Utilities.GetJSONPropertyIfExists(versionElement, "yanked")?.GetBoolean();
                    metadata.VersionUri = $"{ENV_PYPI_ENDPOINT}/project/{purl.Name}/{purl.Version}";
                    metadata.VersionDownloadUri = Utilities.GetJSONPropertyStringIfExists(versionElement, "url");
                }
            }

            return metadata;
        }


        public override List<Version> GetVersions(JsonDocument? contentJSON)
        {
            List<Version> allVersions = new List<Version>();
            if (contentJSON is null) { return allVersions; }

            Console.WriteLine(JsonSerializer.Serialize(contentJSON));
            JsonElement root = contentJSON.RootElement;
            try
            {
                JsonElement versions = root.GetProperty("versions");
                foreach (var version in versions.EnumerateObject())
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
                var versionElement = contentJSON.RootElement.GetProperty("releases").GetProperty(version.ToString());
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

        protected async override Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
        {
            var mapping = new Dictionary<PackageURL, double>();
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
                        string homepage = property.Value.ToString() ?? string.Empty;
                        var packageUrls = GitHubProjectManager.ExtractGitHubPackageURLs(homepage);
                        // if we were able to extract a github url, return
                        if (packageUrls != null && packageUrls.Any())
                        {
                            mapping.Add(packageUrls.First(), 1.0F);
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