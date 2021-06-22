// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using F23.StringSimilarity;
using Microsoft.CST.OpenSource.Model;
using Microsoft.CST.RecursiveExtractor;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Version = SemVer.Version;

namespace Microsoft.CST.OpenSource.Shared
{
    public class BaseProjectManager
    {
        /// <summary>
        ///     Initializes a new project management object.
        /// </summary>
        public BaseProjectManager(string destinationDirectory)
        {
            Options = new Dictionary<string, object>();
            CommonInitialization.OverrideEnvironmentVariables(this);
            TopLevelExtractionDirectory = destinationDirectory;

            if (CommonInitialization.WebClient is HttpClient client)
            {
                WebClient = client;
            }
            else
            {
                throw new NullReferenceException(nameof(WebClient));
            }
        }

        /// <summary>
        ///     Per-object option container.
        /// </summary>
        public Dictionary<string, object> Options { get; private set; }

        /// <summary>
        ///     The location (directory) to extract files to.
        /// </summary>
        public string TopLevelExtractionDirectory { get; set; } = ".";

        /// <summary>
        ///     Extracts GitHub URLs from a given piece of text.
        /// </summary>
        /// <param name="content"> text to analyze </param>
        /// <returns> PackageURLs (type=GitHub) located in the text. </returns>
        public static IEnumerable<PackageURL> ExtractGitHubPackageURLs(string content)
        {
            Logger.Trace("ExtractGitHubPackageURLs({0})", content?.Substring(0, 30));

            if (string.IsNullOrWhiteSpace(content))
            {
                Logger.Debug("Content was empty; nothing to do.");
                return Array.Empty<PackageURL>();
            }
            var purlList = new List<PackageURL>();

            // @TODO: Check the regex below; does this match GitHub's scheme?
            var githubRegex = new Regex(@"github\.com/([a-z0-9\-_\.]+)/([a-z0-9\-_\.]+)",
                                        RegexOptions.IgnoreCase);
            foreach (Match match in githubRegex.Matches(content).Where(match => match != null))
            {
                var user = match.Groups[1].Value;
                var repo = match.Groups[2].Value;

                if (repo.EndsWith(".git", StringComparison.InvariantCultureIgnoreCase))
                {
                    repo = repo[0..^4];
                }

                // False positives, we may need to expand this.
                if (user.Equals("repos", StringComparison.InvariantCultureIgnoreCase) ||
                    user.Equals("metacpan", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // Create a PackageURL from what we know
                var purl = new PackageURL("github", user, repo, null, null, null);
                purlList.Add(purl);
            }

            if (purlList.Count == 0)
            {
                Logger.Debug("No Github URLs were found.");
                return Array.Empty<PackageURL>();
            }
            return purlList.Distinct();
        }

        public static string GetCommonSupportedHelpText()
        {
            var supportedHelpText = @"
The package-url specifier is described at https://github.com/package-url/purl-spec:
  pkg:cargo/rand                The latest version of Rand (via crates.io)
  pkg:cocoapods/AFNetworking    The latest version of AFNetworking (via cocoapods.org)
  pkg:composer/Smarty/Smarty    The latest version of Smarty (via Composer/ Packagist)
  pkg:cpan/Apache-ACEProxy      The latest version of Apache::ACEProxy (via cpan.org)
  pkg:cran/ACNE@0.8.0           Version 0.8.0 of ACNE (via cran.r-project.org)
  pkg:gem/rubytree@*            All versions of RubyTree (via rubygems.org)
  pkg:golang/sigs.k8s.io/yaml   The latest version of sigs.k8s.io/yaml (via proxy.golang.org)
  pkg:github/Microsoft/DevSkim  The latest release of DevSkim (via GitHub)
  pkg:hackage/a50@*             All versions of a50 (via hackage.haskell.org)
  pkg:maven/org.apdplat/deep-qa The latest version of org.apdplat.deep-qa (via repo1.maven.org)
  pkg:npm/express               The latest version of Express (via npm.org)
  pkg:nuget/Newtonsoft.JSON     The latest version of Newtonsoft.JSON (via nuget.org)
  pkg:pypi/django@1.11.1        Version 1.11.1 of Django (via pypi.org)
  pkg:ubuntu/zerofree           The latest version of zerofree from Ubuntu (via packages.ubuntu.com)
  pkg:vsm/MLNET/07              The latest version of MLNET.07 (from marketplace.visualstudio.com)
  pkg:url/foo@1.0?url=<URL>     The direct URL <URL>
";
            return supportedHelpText;
        }

        public static List<string> GetCommonSupportedHelpTextLines()
        {
            return GetCommonSupportedHelpText().Split(Environment.NewLine).ToList<string>();
        }

        /// <summary>
        ///     Retrieves HTTP content from a given URI.
        /// </summary>
        /// <param name="uri"> URI to load. </param>
        /// <returns> </returns>
        public static async Task<string?> GetHttpStringCache(string uri, bool useCache = true, bool neverThrow = false)
        {
            Logger.Trace("GetHttpStringCache({0}, {1})", uri, useCache);

            string? resultString = null;

            try
            {
                if (useCache)
                {
                    lock (DataCache)
                    {
                        if (DataCache.TryGetValue(uri, out string s))
                        {
                            return s;
                        }
                    }
                }

                var result = await WebClient.GetAsync(uri);
                result.EnsureSuccessStatusCode();   // Don't cache error codes
                var contentLength = result.Content.Headers.ContentLength ?? 8192;
                resultString = await result.Content.ReadAsStringAsync();

                if (useCache)
                {
                    lock (DataCache)
                    {
                        var mce = new MemoryCacheEntryOptions() { Size = contentLength };
                        DataCache.Set<string>(uri, resultString, mce);
                    }
                }
            }
            catch (Exception)
            {
                if (!neverThrow)
                {
                    throw;
                }
            }

            return resultString;
        }

        /// <summary>
        ///     Retrieves JSON content from a given URI.
        /// </summary>
        /// <param name="uri"> URI to load. </param>
        /// <returns> Content, as a JsonDocument, possibly from cache. </returns>
        public static async Task<JsonDocument> GetJsonCache(string uri, bool useCache = true)
        {
            Logger.Trace("GetJsonCache({0}, {1})", uri, useCache);
            if (useCache)
            {
                lock (DataCache)
                {
                    if (DataCache.TryGetValue(uri, out JsonDocument js))
                    {
                        return js;
                    }
                }
            }
            Logger.Trace("Loading Uri");
            var result = await WebClient.GetAsync(uri);
            result.EnsureSuccessStatusCode();   // Don't cache error codes
            var contentLength = result.Content.Headers.ContentLength ?? 8192;
            var doc = await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync());

            if (useCache)
            {
                lock (DataCache)
                {
                    var mce = new MemoryCacheEntryOptions() { Size = contentLength };
                    DataCache.Set<JsonDocument>(uri, doc, mce);
                }
            }

            return doc;
        }

        public static IEnumerable<string> SortVersions(IEnumerable<string> versionList)
        {
            if (versionList == null || !versionList.Any())
            {
                return Array.Empty<string>();
            }

            // Split Versions
            var versionPartsList = versionList.Select(s => VersionComparer.Parse(s)).ToList();
            versionPartsList.Sort(new VersionComparer());
            return versionPartsList.Select(s => string.Join("", s));
        }

        /// <summary>
        ///     Downloads a given PackageURL and extracts it locally to a directory.
        /// </summary>
        /// <param name="purl"> PackageURL to download </param>
        /// <returns> Paths (either files or directory names) pertaining to the downloaded files. </returns>
        public virtual Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            throw new NotImplementedException("BaseProjectManager does not implement DownloadVersion.");
        }

        public virtual Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            throw new NotImplementedException("BaseProjectManager does not implement EnumerateVersions.");
        }

        /// <summary>
        ///     Extracts an archive (given by 'bytes') into a directory named 'directoryName', recursively,
        ///     using RecursiveExtractor.
        /// </summary>
        /// <param name="directoryName"> directory to extract content into (within TopLevelExtractionDirectory) </param>
        /// <param name="bytes"> bytes to extract (should be an archive file) </param>
        /// <returns> </returns>
        public async Task<string> ExtractArchive(string directoryName, byte[] bytes, bool cached = false)
        {
            Logger.Trace("ExtractArchive({0}, <bytes> len={1})", directoryName, bytes.Length);

            Directory.CreateDirectory(TopLevelExtractionDirectory);

            if (!cached)
            {
                string fullTargetPath = Path.Combine(TopLevelExtractionDirectory, directoryName);
                while (Directory.Exists(fullTargetPath) || File.Exists(fullTargetPath))
                {
                    directoryName += "-" + DateTime.Now.Ticks;
                    fullTargetPath = Path.Combine(TopLevelExtractionDirectory, directoryName);
                }
            }
            var extractor = new Extractor();
            var extractorOptions = new ExtractorOptions()
            {
                ExtractSelfOnFail = true,
                Parallel = true
                //MaxExtractedBytes = 1000 * 1000 * 10;  // 10 MB maximum package size
            };
            foreach (var fileEntry in extractor.Extract(directoryName, bytes, extractorOptions))
            {
                var fullPath = fileEntry.FullPath.Replace(':', Path.DirectorySeparatorChar);

                // TODO: Does this prevent zip-slip?
                foreach (var c in Path.GetInvalidPathChars())
                {
                    fullPath = fullPath.Replace(c, '-');    // ignore: lgtm [cs/string-concatenation-in-loop]
                }

                var filePathToWrite = Path.Combine(TopLevelExtractionDirectory, fullPath);
                filePathToWrite = filePathToWrite.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                if (Path.GetDirectoryName(filePathToWrite) is string dir && !string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                if (!Directory.Exists(fullPath))
                {
                    using var fs = File.Open(filePathToWrite, FileMode.Append);
                    await fileEntry.Content.CopyToAsync(fs);
                }
            }

            var fullExtractionPath = Path.Combine(TopLevelExtractionDirectory, directoryName);
            fullExtractionPath = Path.GetFullPath(fullExtractionPath);
            Logger.Debug("Archive extracted to {0}", fullExtractionPath);

            return fullExtractionPath;
        }

        /// <summary>
        ///     Gets the latest version from the package metadata
        /// </summary>
        /// <param name="metadata"> </param>
        /// <returns> </returns>
        public Version? GetLatestVersion(JsonDocument? metadata)
        {
            List<Version> versions = GetVersions(metadata);
            return GetLatestVersion(versions);
        }

        /// <summary>
        ///     overload for getting the latest version
        /// </summary>
        /// <param name="versions"> </param>
        /// <returns> </returns>
        public Version? GetLatestVersion(List<Version> versions)
        {
            if (versions?.Count > 0)
            {
                Version? maxVersion = versions.Max();
                return maxVersion;
            }
            return null;
        }

        /// <summary>
        ///     This method should return text reflecting metadata for the given package. There is no assumed format.
        /// </summary>
        /// <param name="purl"> PackageURL to search </param>
        /// <returns> a string containing metadata. </returns>
        public virtual Task<string?> GetMetadata(PackageURL purl)
        {
            var typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetMetadata.");
        }

        /// <summary>
        ///     Get the uri for the package home page (no version)
        /// </summary>
        /// <param name="purl"> </param>
        /// <returns> </returns>
        public virtual Uri? GetPackageAbsoluteUri(PackageURL purl)
        {
            var typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetPackageAbsoluteUri.");
        }

        /// <summary>
        ///     Return a normalized package metadata.
        /// </summary>
        /// <param name="purl"> </param>
        /// <returns> </returns>
        public virtual Task<PackageMetadata> GetPackageMetadata(PackageURL purl)
        {
            var typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetPackageMetadata.");
        }

        /// <summary>
        ///     Gets everything contained in a JSON element for the package version
        /// </summary>
        /// <param name="metadata"> </param>
        /// <param name="version"> </param>
        /// <returns> </returns>
        public virtual JsonElement? GetVersionElement(JsonDocument contentJSON, Version version)
        {
            var typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetVersions.");
        }

        /// <summary>
        ///     Gets all the versions of a package
        /// </summary>
        /// <param name="metadata"> </param>
        /// <param name="version"> </param>
        /// <returns> </returns>
        public virtual List<Version> GetVersions(JsonDocument? metadata)
        {
            var typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetVersions.");
        }

        /// <summary>
        ///     Tries to find out the package repository from the metadata of the package. Check with the
        ///     specific package manager, if they have any specific extraction to do, w.r.t the metadata. If
        ///     they found some package specific well defined metadata, use that. If that doesn't work, do a
        ///     search across the metadata to find probable source repository urls
        /// </summary>
        /// <param name="purl"> PackageURL to search </param>
        /// <returns>
        ///     A dictionary, mapping each possible repo source entry to its probability/empty dictionary
        /// </returns>
        public async Task<Dictionary<PackageURL, double>> IdentifySourceRepository(PackageURL purl)
        {
            Logger.Trace("IdentifySourceRepository({0})", purl);

            var rawMetadataString = await GetMetadata(purl) ?? string.Empty;
            var sourceRepositoryMap = new Dictionary<PackageURL, double>();

            // Check the specific PackageManager-specific implementation first
            try
            {
                foreach (var result in await SearchRepoUrlsInPackageMetadata(purl, rawMetadataString))
                {
                    sourceRepositoryMap.Add(result.Key, result.Value);
                }

                // Return now if we've succeeded
                if (sourceRepositoryMap.Any())
                {
                    return sourceRepositoryMap;
                }
            }
            catch (Exception ex)
            {
                Logger.Trace(ex, "Error searching package metadata for {0}: {1}", purl, ex.Message);
            }

            // Fall back to searching the metadata string for all possible GitHub URLs.
            foreach (var result in ExtractRankedSourceRepositories(purl, rawMetadataString))
            {
                sourceRepositoryMap.Add(result.Key, result.Value);
            }

            return sourceRepositoryMap;
        }

        /// <summary>
        ///     Protected memory cache to make subsequent loads of the same URL fast and transparent.
        /// </summary>
        protected static readonly MemoryCache DataCache = new MemoryCache(
            new MemoryCacheOptions
            {
                SizeLimit = 1024 * 1024 * 8
            }
        );

        /// <summary>
        ///     Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Static HttpClient for use in all HTTP connections.
        ///      Class throws a NullExceptionError in the constructor if this is null.
        /// </summary>
#nullable disable
        protected static HttpClient WebClient;

#nullable enable

        /// <summary>
        ///     Rank the source repo entry candidates by their edit distance.
        /// </summary>
        /// <param name="purl"> the package </param>
        /// <param name="rawMetadataString"> metadata of the package </param>
        /// <returns> Possible candidates of the package/empty dictionary </returns>
        protected Dictionary<PackageURL, double> ExtractRankedSourceRepositories(PackageURL purl, string rawMetadataString)
        {
            Logger.Trace("ExtractRankedSourceRepositories({0})", purl);
            var sourceRepositoryMap = new Dictionary<PackageURL, double>();

            if (purl == null || string.IsNullOrWhiteSpace(rawMetadataString))
            {
                return sourceRepositoryMap;   // Empty
            }

            // Simple regular expression, looking for GitHub URLs
            // TODO: Expand this to Bitbucket, GitLab, etc.
            var sourceUrls = GitHubProjectManager.ExtractGitHubUris(purl, rawMetadataString);
            if (sourceUrls != null && sourceUrls.Any())
            {
                var baseScore = 0.8;     // Max confidence: 0.80
                var levenshtein = new NormalizedLevenshtein();

                foreach (var group in sourceUrls.GroupBy(item => item))
                {
                    // the cumulative boosts should be < 0.2; otherwise it'd be an 1.0 score by Levenshtein distance
                    double similarityBoost = levenshtein.Similarity(purl.Name, group.Key.Name) * 0.0001;

                    // give a similarly weighted boost based on the number of times a particular candidate
                    // appear in the metadata
                    double countBoost = (double)(group.Count()) * 0.0001;

                    sourceRepositoryMap.Add(group.Key, baseScore + similarityBoost + countBoost);
                }
            }
            return sourceRepositoryMap;
        }

        /// <summary>
        ///     Implemented by all package managers to search the metadata, and either return a successful
        ///     result for the package repository, or return a null in case of failure/nothing to do.
        /// </summary>
        /// <returns> </returns>
        protected virtual Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
        {
            return Task.FromResult(new Dictionary<PackageURL, double>());
        }
    }
}