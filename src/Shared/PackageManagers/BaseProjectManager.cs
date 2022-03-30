// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Microsoft.CST.RecursiveExtractor;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.CST.OpenSource.Model;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Utilities;
    using Version = SemanticVersioning.Version;
    using PackageUrl;

    public class BaseProjectManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseProjectManager"/> class.
        /// </summary>
        public BaseProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory, IManagerProvider? managerProvider = null)
        {
            Options = new Dictionary<string, object>();
            TopLevelExtractionDirectory = destinationDirectory;
            HttpClientFactory = httpClientFactory;
            ManagerProvider = managerProvider;
        }

        public BaseProjectManager(string destinationDirectory) : this(new DefaultHttpClientFactory(), destinationDirectory)
        {
        }

        /// <summary>
        /// Per-object option container.
        /// </summary>
        public Dictionary<string, object> Options { get; private set; }

        /// <summary>
        /// The location (directory) to extract files to.
        /// </summary>
        public string TopLevelExtractionDirectory { get; set; } = ".";

        /// <summary>
        /// The <see cref="IHttpClientFactory"/> for the manager.
        /// </summary>
        public IHttpClientFactory HttpClientFactory { get; }
        
        /// <summary>
        /// The <see cref="IManagerProvider"/> for the manager.
        /// </summary>
        public IManagerProvider? ManagerProvider { get; }

        /// <summary>
        /// Extracts GitHub URLs from a given piece of text.
        /// </summary>
        /// <param name="content">text to analyze</param>
        /// <returns>PackageURLs (type=GitHub) located in the text.</returns>
        public static IEnumerable<PackageURL> ExtractGitHubPackageURLs(string content)
        {
            Logger.Trace("ExtractGitHubPackageURLs({0})", content?.Substring(0, 30));

            if (string.IsNullOrWhiteSpace(content))
            {
                Logger.Debug("Content was empty; nothing to do.");
                return Array.Empty<PackageURL>();
            }
            List<PackageURL> purlList = new();

            // @TODO: Check the regex below; does this match GitHub's scheme?
            Regex githubRegex = new(@"github\.com/([a-z0-9\-_\.]+)/([a-z0-9\-_\.]+)",
                                        RegexOptions.IgnoreCase);
            foreach (Match match in githubRegex.Matches(content).Where(match => match != null))
            {
                string user = match.Groups[1].Value;
                string repo = match.Groups[2].Value;

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
                PackageURL purl = new("github", user, repo, null, null, null);
                purlList.Add(purl);
            }

            if (purlList.Count == 0)
            {
                Logger.Debug("No Github URLs were found.");
                return Array.Empty<PackageURL>();
            }
            return purlList.Distinct();
        }

        /// <summary>
        /// Retrieves HTTP content from a given URI.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> to make the request on.</param>
        /// <param name="uri">The URI to load.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <param name="neverThrow">If an exception gets raised, should it not be thrown.</param>
        /// <returns>The string response from the http result content.</returns>
        public static async Task<string?> GetHttpStringCache(HttpClient client, string uri, bool useCache = true, bool neverThrow = false)
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

                HttpResponseMessage result = await client.GetAsync(uri);
                result.EnsureSuccessStatusCode(); // Don't cache error codes.
                long contentLength = result.Content.Headers.ContentLength ?? 8192;
                resultString = await result.Content.ReadAsStringAsync();

                if (useCache)
                {
                    lock (DataCache)
                    {
                        MemoryCacheEntryOptions mce = new() { Size = contentLength };
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
        /// Checks <see cref="GetHttpStringCache(HttpClient, string, bool, bool)"/> to see if the package exists.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> to make the request on.</param>
        /// <param name="url">The URL to check.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <returns>true if the package exists.</returns>
        internal static async Task<bool> CheckHttpCacheForPackage(HttpClient client, string url, bool useCache = true)
        {
            Logger.Trace("CheckHttpCacheForPackage {0}", url);
            try
            {
                // GetHttpStringCache throws an exception if it has trouble finding the package.
                _ = await GetHttpStringCache(client, url, useCache);
                return true;
            }
            catch (Exception e)
            {
                if (e is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
                {
                    Logger.Trace("Package not found at: {0}", url);
                    return false;
                }
                Logger.Debug("Unable to check if package {1} exists: {0}", e.Message, url);
            }
            return false;
        }

        /// <summary>
        /// Checks <see cref="GetJsonCache(HttpClient, string, bool)"/> to see if the package exists.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> to make the request on.</param>
        /// <param name="url">The URL to check.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <returns>true if the package exists.</returns>
        internal static async Task<bool> CheckJsonCacheForPackage(HttpClient client, string url, bool useCache = true)
        {
            Logger.Trace("CheckJsonCacheForPackage {0}", url);
            try
            {
                // GetJsonCache throws an exception if it has trouble finding the package.
                _ = await GetJsonCache(client, url, useCache);
                return true;
            }
            catch (Exception e)
            {
                if (e is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
                {
                    Logger.Trace("Package not found at: {0}", url);
                    return false;
                }
                Logger.Debug("Unable to check if package {1} exists: {0}", e.Message, url);
            }
            return false;
        }

        /// <summary>
        /// Retrieves JSON content from a given URI.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> to make the request on.</param>
        /// <param name="uri">URI to load.</param>
        /// <param name="useCache">If cache should be used. If false will make a direct WebClient request.</param>
        /// <returns>Content, as a JsonDocument, possibly from cache.</returns>
        public static async Task<JsonDocument> GetJsonCache(HttpClient client, string uri, bool useCache = true)
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
            Logger.Trace("Loading Uri...");
            HttpResponseMessage result = await client.GetAsync(uri);
            result.EnsureSuccessStatusCode(); // Don't cache error codes.
            long contentLength = result.Content.Headers.ContentLength ?? 8192;
            JsonDocument doc = await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync());

            if (useCache)
            {
                lock (DataCache)
                {
                    MemoryCacheEntryOptions? mce = new() { Size = contentLength };
                    DataCache.Set<JsonDocument>(uri, doc, mce);
                }
            }

            return doc;
        }

        /// <summary>
        /// Sorts the versions of a package in descending order.
        /// </summary>
        /// <param name="versionList">The available list of versions on a package.</param>
        /// <returns>The sorted list of versions.</returns>
        public static IEnumerable<string> SortVersions(IEnumerable<string> versionList)
        {
            List<string> versionListEnumerated = versionList.ToList();
            if (!versionListEnumerated.Any())
            {
                return Array.Empty<string>();
            }

            // Split Versions
            List<List<string>> versionPartsList = versionListEnumerated.Select(VersionComparer.Parse).ToList();
            versionPartsList.Sort(new VersionComparer());
            return versionPartsList.Select(s => string.Join("", s));
        }

        /// <summary>
        /// Downloads a given PackageURL and extracts it locally to a directory.
        /// </summary>
        /// <param name="purl">PackageURL to download</param>
        /// <returns>Paths (either files or directory names) pertaining to the downloaded files.</returns>
        public virtual Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            throw new NotImplementedException("BaseProjectManager does not implement DownloadVersion.");
        }

        /// <summary>
        /// Enumerates all possible versions of the package identified by purl, in descending order.
        /// </summary>
        /// <remarks>The latest version is always first, then it is sorted by SemVer in descending order.</remarks>
        /// <param name="purl">Package URL specifying the package. Version is ignored.</param>
        /// <param name="useCache">If the cache should be used when looking for the versions.</param>
        /// <returns> A list of package version numbers.</returns>
        public virtual Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            throw new NotImplementedException("BaseProjectManager does not implement EnumerateVersions.");
        }

        /// <summary>
        /// Extracts an archive (given by 'bytes') into a directory named 'directoryName',
        /// recursively, using RecursiveExtractor.
        /// </summary>
        /// <param name="topLevelDirectory">The top level directory content should be extracted to.</param>
        /// <param name="directoryName">directory to extract content into (within <paramref name="topLevelDirectory"/>)</param>
        /// <param name="bytes">bytes to extract (should be an archive file)</param>
        /// <param name="cached">If the archive has been cached.</param>
        /// <returns></returns>
        public static async Task<string> ExtractArchive(string topLevelDirectory, string directoryName, byte[] bytes, bool cached = false)
        {
            Logger.Trace("ExtractArchive({0}, <bytes> len={1})", directoryName, bytes.Length);

            Directory.CreateDirectory(topLevelDirectory);

            StringBuilder dirBuilder = new(directoryName);

            foreach (char c in Path.GetInvalidPathChars())
            {
                dirBuilder.Replace(c, '-');    // ignore: lgtm [cs/string-concatenation-in-loop]
            }

            string fullTargetPath = Path.Combine(topLevelDirectory, dirBuilder.ToString());

            if (!cached)
            {
                while (Directory.Exists(fullTargetPath) || File.Exists(fullTargetPath))
                {
                    dirBuilder.Append("-" + DateTime.Now.Ticks);
                    fullTargetPath = Path.Combine(topLevelDirectory, dirBuilder.ToString());
                }
            }
            Extractor extractor = new();
            ExtractorOptions extractorOptions = new()
            {
                ExtractSelfOnFail = true,
                Parallel = true
                // MaxExtractedBytes = 1000 * 1000 * 10;  // 10 MB maximum package size
            };
            ExtractionStatusCode result = await extractor.ExtractToDirectoryAsync(topLevelDirectory, dirBuilder.ToString(), new MemoryStream(bytes), extractorOptions);
            if (result == ExtractionStatusCode.Ok)
            {
                Logger.Debug("Archive extracted to {0}", fullTargetPath);
            }
            else
            {
                Logger.Warn("Error extracting archive {0} ({1})", fullTargetPath, result);
            }

            return fullTargetPath;
        }

        /// <summary>
        /// Gets the latest version from the package metadata.
        /// </summary>
        /// <param name="metadata">The package metadata to parse.</param>
        /// <returns>The latest version of the package.</returns>
        public Version? GetLatestVersion(JsonDocument metadata)
        {
            List<Version> versions = GetVersions(metadata);
            return GetLatestVersion(versions);
        }

        /// <summary>
        /// Check if the package exists in the repository.
        /// </summary>
        /// <param name="purl">The PackageURL to check.</param>
        /// <param name="useCache">If the cache should be checked for the existence of this package.</param>
        /// <returns>True if the package is confirmed to exist in the repository. False otherwise.</returns>
        public virtual async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (purl is null)
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            return (await EnumerateVersions(purl, useCache)).Any();
        }

        /// <summary>
        /// Static overload for getting the latest version.
        /// </summary>
        /// <param name="versions">The list of versions.</param>
        /// <returns>The latest version from the list.</returns>
        public static Version? GetLatestVersion(List<Version> versions)
        {
            if (versions.Any())
            {
                Version? maxVersion = versions.Max();
                return maxVersion;
            }
            return null;
        }

        /// <summary>
        /// This method should return text reflecting metadata for the given package. There is no
        /// assumed format.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> to get the metadata for.</param>
        /// <param name="useCache">If the metadata should be retrieved from the cache, if it is available.</param>
        /// <remarks>If no version specified, defaults to latest version.</remarks>
        /// <returns>A string representing the <see cref="PackageURL"/>'s metadata.</returns>
        public virtual Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
        {
            throw new NotImplementedException($"{GetType().Name} does not implement GetMetadata.");
        }

        /// <summary>
        /// Get the uri for the package home page (no version)
        /// </summary>
        /// <param name="purl"></param>
        /// <returns></returns>
        public virtual Uri? GetPackageAbsoluteUri(PackageURL purl)
        {
            throw new NotImplementedException($"{GetType().Name} does not implement GetPackageAbsoluteUri.");
        }

        /// <summary>
        /// Return a normalized package metadata.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> to get the normalized metadata for.</param>
        /// <param name="useCache">If the <see cref="PackageMetadata"/> should be retrieved from the cache, if it is available.</param>
        /// <remarks>If no version specified, defaults to latest version.</remarks>
        /// <returns>A <see cref="PackageMetadata"/> object representing this <see cref="PackageURL"/>.</returns>
        public virtual Task<PackageMetadata> GetPackageMetadata(PackageURL purl, bool useCache = true)
        {
            string typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetPackageMetadata.");
        }

        /// <summary>
        /// Gets everything contained in a JSON element for the package version
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public virtual JsonElement? GetVersionElement(JsonDocument contentJSON, Version version)
        {
            string typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetVersions.");
        }

        /// <summary>
        /// Gets all the versions of a package
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public virtual List<Version> GetVersions(JsonDocument? metadata)
        {
            string typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetVersions.");
        }

        /// <summary>
        /// Tries to find out the package repository from the metadata of the package. Check with
        /// the specific package manager, if they have any specific extraction to do, w.r.t the
        /// metadata. If they found some package specific well defined metadata, use that. If that
        /// doesn't work, do a search across the metadata to find probable source repository urls
        /// </summary>
        /// <param name="purl">PackageURL to search</param>
        /// <param name="useCache">If the source repository should be returned from the cache, if available.</param>
        /// <returns>
        /// A dictionary, mapping each possible repo source entry to its probability/empty dictionary
        /// </returns>
        public async Task<Dictionary<PackageURL, double>> IdentifySourceRepository(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("IdentifySourceRepository({0})", purl);

            string rawMetadataString = await GetMetadata(purl, useCache) ?? string.Empty;
            Dictionary<PackageURL, double> sourceRepositoryMap = new();

            // Check the specific PackageManager-specific implementation first
            try
            {
                foreach (KeyValuePair<PackageURL, double> result in await SearchRepoUrlsInPackageMetadata(purl, rawMetadataString))
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
            foreach (KeyValuePair<PackageURL, double> result in ExtractRankedSourceRepositories(purl, rawMetadataString))
            {
                sourceRepositoryMap.Add(result.Key, result.Value);
            }

            return sourceRepositoryMap;
        }

        /// <summary>
        /// Protected memory cache to make subsequent loads of the same URL fast and transparent.
        /// </summary>
        protected static readonly MemoryCache DataCache = new(
            new MemoryCacheOptions
            {
                SizeLimit = 1024 * 1024 * 8
            }
        );

        /// <summary>
        /// Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Rank the source repo entry candidates by their edit distance.
        /// </summary>
        /// <param name="purl">the package</param>
        /// <param name="rawMetadataString">metadata of the package</param>
        /// <returns>Possible candidates of the package/empty dictionary</returns>
        protected static Dictionary<PackageURL, double> ExtractRankedSourceRepositories(PackageURL purl, string rawMetadataString)
        {
            Logger.Trace("ExtractRankedSourceRepositories({0})", purl);
            Dictionary<PackageURL, double> sourceRepositoryMap = new();

            if (purl == null || string.IsNullOrWhiteSpace(rawMetadataString))
            {
                return sourceRepositoryMap;   // Empty
            }

            // Simple regular expression, looking for GitHub URLs
            // TODO: Expand this to Bitbucket, GitLab, etc.
            // TODO: Bring this back, but like better.
            /*IEnumerable<PackageURL> sourceUrls = GitHubProjectManager.ExtractGitHubUris(purl, rawMetadataString);
            if (sourceUrls != null && sourceUrls.Any())
            {
                double baseScore = 0.8;     // Max confidence: 0.80
                NormalizedLevenshtein levenshtein = new();

                foreach (IGrouping<PackageURL, PackageURL>? group in sourceUrls.GroupBy(item => item))
                {
                    // the cumulative boosts should be < 0.2; otherwise it'd be an 1.0 score by
                    // Levenshtein distance
                    double similarityBoost = levenshtein.Similarity(purl.Name, group.Key.Name) * 0.0001;

                    // give a similarly weighted boost based on the number of times a particular
                    // candidate appear in the metadata
                    double countBoost = group.Count() * 0.0001;

                    sourceRepositoryMap.Add(group.Key, baseScore + similarityBoost + countBoost);
                }
            }*/
            return sourceRepositoryMap;
        }

        /// <summary>
        /// Implemented by all package managers to search the metadata, and either return a
        /// successful result for the package repository, or return a null in case of
        /// failure/nothing to do.
        /// </summary>
        /// <returns></returns>
        protected virtual Task<Dictionary<PackageURL, double>> SearchRepoUrlsInPackageMetadata(PackageURL purl, string metadata)
        {
            return Task.FromResult(new Dictionary<PackageURL, double>());
        }

        protected HttpClient CreateHttpClient()
        {
            return HttpClientFactory.CreateClient(GetType().Name);
        }
    }
}