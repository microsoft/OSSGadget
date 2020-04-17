// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Version = SemVer.Version;

namespace Microsoft.CST.OpenSource.Shared
{
    class NPMProjectManager : BaseProjectManager
    {
        private static readonly List<string> npm_internal_modules = new List<string>()
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

        public static string ENV_NPM_ENDPOINT = "https://registry.npmjs.org";

        /// <summary>
        /// Download one NPM package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
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
                var doc = await GetJsonCache($"{ENV_NPM_ENDPOINT}/{packageName}");
                var tarball = doc.RootElement.GetProperty("versions").GetProperty(packageVersion).GetProperty("dist").GetProperty("tarball").GetString();
                var result = await WebClient.GetAsync(tarball);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl.ToString());
                var targetName = $"npm-{packageName}@{packageVersion}";
                if (doExtract)
                {
                    downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync()));
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
                Logger.Error(ex, "Error downloading NPM package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            try
            {
                var packageName = purl.Name;
                var doc = await GetJsonCache($"{ENV_NPM_ENDPOINT}/{packageName}");
                var versionList = new List<string>();

                foreach (var versionKey in doc.RootElement.GetProperty("versions").EnumerateObject())
                {
                    Logger.Debug("Identified {0} version {1}.", packageName, versionKey.Name);
                    versionList.Add(versionKey.Name);
                }
                var latestVersion = doc.RootElement.GetProperty("dist-tags").GetProperty("latest").GetString();
                Logger.Debug("Identified {0} version {1}.", packageName, latestVersion);
                versionList.Add(latestVersion);

                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating NPM package: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public override async Task<string> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageName = purl.Name;
                var content = await GetHttpStringCache($"{ENV_NPM_ENDPOINT}/{packageName}");
                return content;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching NPM metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Searches the package manager metadata to figure out the source code repository
        /// </summary>
        /// <param name="purl">the package for which we need to find the source code repository</param>
        /// <returns>A dictionary, mapping each possible repo source entry to its probability/empty dictionary</returns>
        protected async override Task<Dictionary<PackageURL, double>> PackageMetadataSearch(PackageURL purl, 
            string metadata)
        {
            var mapping = new Dictionary<PackageURL, double>();
            if (purl.Name.StartsWith('_') || npm_internal_modules.Contains(purl.Name))
            {
                // url = 'https://github.com/nodejs/node/tree/master/lib' + package.name,

                mapping.Add(new PackageURL(purl.Type, purl.Namespace, purl.Name, 
                    null, null, "node/tree/master/lib"), 1.0F);
                return mapping;
            }
            if (string.IsNullOrEmpty(metadata))
            {
                return mapping;
            }
            JsonDocument contentJSON = JsonDocument.Parse(metadata);

            // if a version is provided, search that JSONElement, otherwise, just search the latest version,
            // which is more likely best maintained
            // TODO: If the latest version JSONElement doesnt have the repo infor, should we search all elements 
            // on that chance that one of them might have it?
            JsonElement versionJSON = string.IsNullOrEmpty(purl.Version) ? GetLatestVersionElement(contentJSON) : 
                GetVersionElement(contentJSON, new Version(purl.Version));

            try
            {
                JsonElement repositoryJSON = versionJSON.GetProperty("repository");
                string repoType = repositoryJSON.GetProperty("type").ToString().ToLower();
                string repoURL = repositoryJSON.GetProperty("url").ToString();

                // right now we deal with only github repos
                if (repoType == "git")
                {
                    PackageURL gitPURL = GitHubProjectManager.ParseUri(new Uri(repoURL));
                    // we got a repository value the author specified in the metadata - 
                    // so no further processing needed
                    if (gitPURL != null)
                    {
                        mapping.Add(gitPURL, 1.0F);
                        return mapping;
                    }
                }
            }
            catch (KeyNotFoundException) { /* continue onwards */ }
            catch (UriFormatException) {  /* the uri specified in the metadata invalid */ }

            return mapping;
        }

        public List<Version> GetVersions(JsonDocument contentJSON)
        {
            List<Version> allVersions = new List<Version>();
            JsonElement root = contentJSON.RootElement;
            try
            {
                JsonElement versions = root.GetProperty("versions");
                foreach (var version in versions.EnumerateObject())
                {
                    allVersions.Add(new Version(version.Name));
                }
            }
            catch (KeyNotFoundException) { return default; }
            catch (InvalidOperationException) { return default; }

            return allVersions;
        }

        /// <summary>
        /// Gets the latest version of the package
        /// </summary>
        /// <param name="contentJSON"></param>
        /// <returns></returns>
        public JsonElement GetLatestVersionElement(JsonDocument contentJSON)
        {
            List<Version> versions = GetVersions(contentJSON);
            Version maxVersion = versions.Max();
            return GetVersionElement(contentJSON, maxVersion);
        }

        public JsonElement GetVersionElement(JsonDocument contentJSON, Version version)
        {
            JsonElement root = contentJSON.RootElement;

            try
            {
                JsonElement versionsJSON = root.GetProperty("versions");
                foreach (JsonProperty versionProperty in versionsJSON.EnumerateObject())
                {
                    if (versionProperty.Name == version.ToString())
                    {
                        return versionsJSON.GetProperty(version.ToString());
                    }
                }
            }
            catch (KeyNotFoundException) { return default; }
            catch (InvalidOperationException) { return default; }

            return default;
        }
    }
}
