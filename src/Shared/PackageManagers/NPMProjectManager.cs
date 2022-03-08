// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Microsoft.CST.OpenSource.Model;
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

    public class NPMProjectManager : BaseProjectManager
    {
        public static string ENV_NPM_API_ENDPOINT { get; set; } = "https://registry.npmjs.org";
        public static string ENV_NPM_ENDPOINT { get; set; } = "https://www.npmjs.com";

        public NPMProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public NPMProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        /// Download one NPM package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            // shouldn't happen here, but check
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                HttpClient httpClient = CreateHttpClient();
                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_NPM_API_ENDPOINT}/{packageName}");
                string? tarball = doc.RootElement.GetProperty("versions").GetProperty(packageVersion).GetProperty("dist").GetProperty("tarball").GetString();
                HttpResponseMessage result = await httpClient.GetAsync(tarball);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl?.ToString());
                string targetName = $"npm-{packageName}@{packageVersion}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory ?? string.Empty, targetName);
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
                    targetName += Path.GetExtension(tarball) ?? "";
                    await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(targetName);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading NPM package: {0}", ex.Message);
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

            return await CheckJsonCacheForPackage(httpClient, $"{ENV_NPM_API_ENDPOINT}/{packageName}", useCache);
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
                string packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();

                JsonDocument doc = await GetJsonCache(httpClient, $"{ENV_NPM_API_ENDPOINT}/{packageName}");
                List<string> versionList = new();

                foreach (JsonProperty versionKey in doc.RootElement.GetProperty("versions").EnumerateObject())
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, versionKey.Name);
                    versionList.Add(versionKey.Name);
                }
                string? latestVersion = doc.RootElement.GetProperty("dist-tags").GetProperty("latest").GetString();
                if (!string.IsNullOrWhiteSpace(latestVersion))
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, latestVersion);
                    versionList.Add(latestVersion);
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the latest version of the package
        /// </summary>
        /// <param name="contentJSON"></param>
        /// <returns></returns>
        public JsonElement? GetLatestVersionElement(JsonDocument contentJSON)
        {
            List<Version> versions = GetVersions(contentJSON);
            Version? maxVersion = GetLatestVersion(versions);
            if (maxVersion is null) { return null; }
            return GetVersionElement(contentJSON, maxVersion);
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                string? packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, $"{ENV_NPM_API_ENDPOINT}/{packageName}");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching NPM metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_NPM_API_ENDPOINT}/{purl?.Name}");
        }

        /// <inheritdoc />
        public override async Task<PackageMetadata> GetPackageMetadata(PackageURL purl)
        {
            PackageMetadata metadata = new();
            string? content = await GetMetadata(purl);
            if (string.IsNullOrEmpty(content)) { return metadata; }

            // convert NPM package data to normalized form
            JsonDocument contentJSON = JsonDocument.Parse(content);
            JsonElement root = contentJSON.RootElement;

            metadata.Name = root.GetProperty("name").GetString();
            metadata.Description = root.GetProperty("description").GetString();

            metadata.PackageManagerUri = ENV_NPM_ENDPOINT;
            metadata.Platform = "NPM";
            metadata.Language = "JavaScript";
            metadata.PackageUri = $"{metadata.PackageManagerUri}/package/{metadata.Name}";
            metadata.ApiPackageUri = $"{ENV_NPM_API_ENDPOINT}/{metadata.Name}";

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
            if (metadata.PackageVersion != null)
            {
                Version versionToGet = new(metadata.PackageVersion);
                JsonElement? versionElement = GetVersionElement(contentJSON, versionToGet);
                if (versionElement != null)
                {
                    // redo the generic values to version specific values
                    metadata.PackageUri = $"{ENV_NPM_ENDPOINT}/package/{metadata.Name}";
                    metadata.VersionUri = $"{ENV_NPM_ENDPOINT}/package/{metadata.Name}/v/{metadata.PackageVersion}";
                    metadata.ApiVersionUri = $"{ENV_NPM_API_ENDPOINT}/{metadata.Name}/{metadata.PackageVersion}";

                    JsonElement? distElement = OssUtilities.GetJSONPropertyIfExists(versionElement, "dist");
                    if (distElement?.GetProperty("tarball") is JsonElement tarballElement)
                    {
                        metadata.VersionDownloadUri = tarballElement.ToString() ??
                        $"{ENV_NPM_API_ENDPOINT}/{metadata.Name}/-/{metadata.Name}-{metadata.PackageVersion}.tgz";
                    }

                    if (distElement?.GetProperty("integrity") is JsonElement integrityElement &&
                        integrityElement.ToString() is string integrity &&
                        integrity.Split('-') is string[] pair &&
                        pair.Length == 2)
                    {
                        metadata.Signature ??= new List<Digest>();
                        metadata.Signature.Add(new Digest()
                        {
                            Algorithm = pair[0],
                            Signature = pair[1]
                        });
                    }
                    
                    // check for typescript
                    List<string>? devDependencies = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "devDependencies"));
                    if (devDependencies is not null && devDependencies.Count > 0 && devDependencies.Any(stringToCheck => stringToCheck.Contains("\"typescript\":")))
                    {
                        metadata.Language = "TypeScript";
                    }
                    
                    // size
                    if (OssUtilities.GetJSONPropertyIfExists(distElement, "unpackedSize") is JsonElement sizeElement &&
                        sizeElement.GetInt64() is long size)
                    {
                        metadata.Size = size;
                    }

                    // homepage
                    if (OssUtilities.GetJSONPropertyStringIfExists(versionElement, "homepage") is string homepage &&
                        !string.IsNullOrWhiteSpace(homepage))
                    {
                        metadata.Homepage = homepage;
                    }
                    
                    // commit id
                    if (OssUtilities.GetJSONPropertyStringIfExists(versionElement, "gitHead") is string gitHead &&
                        !string.IsNullOrWhiteSpace(gitHead))
                    {
                        metadata.CommitId = gitHead;
                    }

                    // install scripts
                    List<string>? scripts = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "scripts"));
                    if (scripts is not null && scripts.Count > 0)
                    {
                        metadata.Scripts ??= new List<Command>();
                        scripts.ForEach((element) => metadata.Scripts.Add(new Command { CommandLine = element }));
                    }

                    // dependencies
                    List<string>? dependencies = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "dependencies"));
                    if (dependencies is not null && dependencies.Count > 0)
                    {
                        metadata.Dependencies ??= new List<Dependency>();
                        dependencies.ForEach((dependency) => metadata.Dependencies.Add(new Dependency() { Package = dependency }));
                    }

                    // author(s)
                    JsonElement? authorElement = OssUtilities.GetJSONPropertyIfExists(versionElement, "author");
                    User author = new();
                    if (authorElement is not null)
                    {
                        author.Name = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "name");
                        author.Email = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "email");
                        author.Url = OssUtilities.GetJSONPropertyStringIfExists(authorElement, "url");

                        metadata.Authors ??= new List<User>();
                        metadata.Authors.Add(author);
                    }

                    // maintainers
                    JsonElement? maintainersElement = OssUtilities.GetJSONPropertyIfExists(versionElement, "maintainers");
                    if (maintainersElement?.EnumerateArray() is JsonElement.ArrayEnumerator maintainerEnumerator)
                    {
                        metadata.Maintainers ??= new List<User>();
                        maintainerEnumerator.ToList().ForEach((element) =>
                        {
                            metadata.Maintainers.Add(
                                new User
                                {
                                    Name = OssUtilities.GetJSONPropertyStringIfExists(element, "name"),
                                    Email = OssUtilities.GetJSONPropertyStringIfExists(element, "email"),
                                    Url = OssUtilities.GetJSONPropertyStringIfExists(element, "url")
                                });
                        });
                    }

                    // repository
                    Dictionary<PackageURL, double> repoMappings = await SearchRepoUrlsInPackageMetadata(purl, content);
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

                    // keywords
                    metadata.Keywords = OssUtilities.ConvertJSONToList(OssUtilities.GetJSONPropertyIfExists(versionElement, "keywords"));

                    // licenses
                    {
                        if (OssUtilities.GetJSONEnumerator(OssUtilities.GetJSONPropertyIfExists(versionElement, "licenses"))
                                is JsonElement.ArrayEnumerator enumeratorElement &&
                            enumeratorElement.ToList() is List<JsonElement> enumerator &&
                            enumerator.Any())
                        {
                            metadata.Licenses ??= new List<License>();
                            // TODO: Convert/append SPIX_ID values?
                            enumerator.ForEach((license) =>
                            {
                                metadata.Licenses.Add(new License()
                                {
                                    Name = OssUtilities.GetJSONPropertyStringIfExists(license, "type"),
                                    Url = OssUtilities.GetJSONPropertyStringIfExists(license, "url")
                                });
                            });
                        }
                    }
                }
            }

            if (latestVersion is not null)
            {
                metadata.LatestPackageVersion = latestVersion.ToString();
            }

            return metadata;
        }

        public override JsonElement? GetVersionElement(JsonDocument? contentJSON, Version version)
        {
            if (contentJSON is null) { return null; }
            JsonElement root = contentJSON.RootElement;

            try
            {
                JsonElement versionsJSON = root.GetProperty("versions");
                foreach (JsonProperty versionProperty in versionsJSON.EnumerateObject())
                {
                    if (string.Equals(versionProperty.Name, version.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        return versionsJSON.GetProperty(version.ToString());
                    }
                }
            }
            catch (KeyNotFoundException) { return null; }
            catch (InvalidOperationException) { return null; }

            return null;
        }

        public override List<Version> GetVersions(JsonDocument? contentJSON)
        {
            List<Version> allVersions = new();
            if (contentJSON is null) { return allVersions; }

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

        /// <summary>
        /// Searches the package manager metadata to figure out the source code repository
        /// </summary>
        /// <param name="purl">the package for which we need to find the source code repository</param>
        /// <returns>
        /// A dictionary, mapping each possible repo source entry to its probability/empty dictionary
        /// </returns>

        protected override async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl,
            string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
            {
                return new Dictionary<PackageURL, double>();
            }
            JsonDocument contentJSON = JsonDocument.Parse(metadata);
            return await SearchRepoUrlsInPackageMetadata(purl, contentJSON);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected async Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl,
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            JsonDocument contentJSON)
        {
            Dictionary<PackageURL, double>? mapping = new();
            if (purl.Name is string purlName && (purlName.StartsWith('_') || npm_internal_modules.Contains(purlName)))
            {
                // url = 'https://github.com/nodejs/node/tree/master/lib' + package.name,

                mapping.Add(new PackageURL(purl.Type, purl.Namespace, purl.Name,
                    null, null, "node/tree/master/lib"), 1.0F);
                return mapping;
            }

            // if a version is provided, search that JSONElement, otherwise, just search the latest
            // version, which is more likely best maintained
            // TODO: If the latest version JSONElement doesnt have the repo infor, should we search all elements
            // on that chance that one of them might have it?
            JsonElement? versionJSON = string.IsNullOrEmpty(purl?.Version) ? GetLatestVersionElement(contentJSON) :
                GetVersionElement(contentJSON, new Version(purl.Version));

            if (versionJSON is JsonElement notNullVersionJSON)
            {
                try
                {
                    JsonElement repositoryJSON = notNullVersionJSON.GetProperty("repository");
                    string? repoType = OssUtilities.GetJSONPropertyStringIfExists(repositoryJSON, "type")?.ToLower();
                    string? repoURL = OssUtilities.GetJSONPropertyStringIfExists(repositoryJSON, "url");

                    // right now we deal with only github repos
                    if (repoType == "git" && repoURL is not null)
                    {
                        PackageURL gitPURL = GitHubProjectManager.ParseUri(new Uri(repoURL));
                        // we got a repository value the author specified in the metadata - so no
                        // further processing needed
                        if (gitPURL != null)
                        {
                            mapping.Add(gitPURL, 1.0F);
                            return mapping;
                        }
                    }
                }
                catch (KeyNotFoundException) { /* continue onwards */ }
                catch (UriFormatException) {  /* the uri specified in the metadata invalid */ }
            }

            return mapping;
        }

        /// <summary>
        /// Internal Node.js modules that should be ignored when searching metadata.
        /// </summary>
        private static readonly List<string> npm_internal_modules = new()
        {
            "assert",
            "async_hooks",
            "buffer",
            "child_process",
            "cluster",
            "console",
            "constants",
            "crypto",
            "dgram",
            "dns",
            "domain",
            "events",
            "fs",
            "http",
            "http2",
            "https",
            "inspector",
            "module",
            "net",
            "os",
            "path",
            "perf_hooks",
            "process",
            "punycode",
            "querystring",
            "readline",
            "repl",
            "stream",
            "string_decoder",
            "timers",
            "tls",
            "trace_events",
            "tty",
            "url",
            "util",
            "v8",
            "vm",
            "zlib"
        };
    }
}