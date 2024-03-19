// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Helpers;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.CST.OpenSource.Model;
    using Model.Enums;
    using Model.PackageExistence;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Utilities;
    using Version = SemanticVersioning.Version;
    using PackageUrl;
    using System.IO;

    public abstract class BaseProjectManager : IBaseProjectManager
    {
        /// <inheritdoc />
        public abstract string ManagerType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseProjectManager"/> class.
        /// </summary>
        public BaseProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory = ".")
        {
            EnvironmentHelper.OverrideEnvironmentVariables(this);
            Options = new Dictionary<string, object>();
            TopLevelExtractionDirectory = destinationDirectory;
            HttpClientFactory = httpClientFactory;
        }

        public BaseProjectManager(string destinationDirectory = ".") : this(new DefaultHttpClientFactory(), destinationDirectory)
        {
        }

        /// <inheritdoc />
        public Dictionary<string, object> Options { get; private set; }

        /// <inheritdoc />
        public string TopLevelExtractionDirectory { get; init; }

        /// <inheritdoc />
        public IHttpClientFactory HttpClientFactory { get; }

        /// <summary>
        /// Extracts GitHub URLs from a given piece of text.
        /// </summary>
        /// <param name="content">text to analyze</param>
        /// <returns>PackageURLs (type=GitHub) located in the text.</returns>
        public static IEnumerable<PackageURL> ExtractGitHubPackageURLs(string content)
        {
            Logger.Trace("ExtractGitHubPackageURLs({0})", content?.Take(30));

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
                resultString = await result.Content.ReadAsStringAsync();
                long contentLength = resultString.Length;

                if (useCache)
                {
                    lock (DataCache)
                    {
                        MemoryCacheEntryOptions mce = new() { 
                            Size = contentLength,
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        };
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
        /// <param name="jsonParsingOption">Any special json parsing rules.</param>
        /// <returns>true if the package exists.</returns>
        internal static async Task<bool> CheckJsonCacheForPackage(HttpClient client, string url, bool useCache = true, JsonParsingOption jsonParsingOption = JsonParsingOption.None)
        {
            Logger.Trace("CheckJsonCacheForPackage {0}", url);
            try
            {
                // GetJsonCache throws an exception if it has trouble finding the package.
                _ = await GetJsonCache(client, url, useCache, jsonParsingOption);
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
        /// <param name="jsonParsingOption">Any special json parsing rules.</param>
        /// <returns>Content, as a JsonDocument, possibly from cache.</returns>
        public static async Task<JsonDocument> GetJsonCache(HttpClient client, string uri, bool useCache = true, JsonParsingOption jsonParsingOption = JsonParsingOption.None)
        {
            Logger.Trace("GetJsonCache({0}, {1})", uri, useCache);
            var cacheKey = $"{uri}/json";
            if (useCache)
            {
                lock (DataCache)
                {
                    if (DataCache.TryGetValue(cacheKey, out JsonDocument js))
                    {
                        return js;
                    }
                }
            }
            Logger.Trace("Loading Uri...");
            HttpResponseMessage result = await client.GetAsync(uri);
            result.EnsureSuccessStatusCode(); // Don't cache error codes.
            long contentLength = 0;
            JsonDocument doc;

            switch (jsonParsingOption)
            {
                case JsonParsingOption.NotInArrayNotCsv:
                    string data = await result.Content.ReadAsStringAsync();
                    contentLength = data.Length;
                    data = Regex.Replace(data, @"\r\n?|\n", ",");
                    data = $"[{data}]";

                    doc = JsonDocument.Parse(data, new JsonDocumentOptions()
                    {
                        AllowTrailingCommas = true,
                    });
                    break;
                default:
                    Stream responseStream = await result.Content.ReadAsStreamAsync();
                    contentLength = responseStream.Length;
                    doc = await JsonDocument.ParseAsync(responseStream);
                    break;
            }
            
            if (useCache)
            {
                lock (DataCache)
                {
                    MemoryCacheEntryOptions? mce = new() 
                    {
                        Size = contentLength,
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                    };
                    DataCache.Set<JsonDocument>(cacheKey, doc, mce);
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

        /// <inheritdoc />
        public virtual Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            throw new NotImplementedException("BaseProjectManager does not implement DownloadVersionAsync.");
        }

        /// <inheritdoc />
        public abstract Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true);

        /// <inheritdoc />
        public Version? GetLatestVersion(JsonDocument metadata)
        {
            List<Version> versions = GetVersions(metadata);
            return GetLatestVersion(versions);
        }

        /// <inheritdoc />
        public virtual async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (purl is null)
            {
                Logger.Trace("Provided PackageURL was null.");
                throw new ArgumentNullException(nameof(purl), "Provided PackageURL was null.");
            }
            return (await EnumerateVersionsAsync(purl, useCache)).Any();
        }
        
        /// <inheritdoc />
        public virtual async Task<IPackageExistence> DetailedPackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            bool exists = await PackageExistsAsync(purl, useCache);

            if (exists)
            {
                return new PackageExists();
            }

            return new PackageNotFound();
        }

        /// <inheritdoc />
        public virtual async Task<bool> PackageVersionExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (purl is null)
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }

            if(purl.Version.IsBlank())
            {
                Logger.Trace("Provided PackageURL version was null or blank.");
                return false;
            }

            return (await EnumerateVersionsAsync(purl, useCache)).Contains(purl.Version);
        }
        
        /// <inheritdoc />
        public virtual async Task<IPackageExistence> DetailedPackageVersionExistsAsync(PackageURL purl, bool useCache = true)
        {
            bool exists = await PackageVersionExistsAsync(purl, useCache);

            if (exists)
            {
                return new PackageVersionExists();
            }

            return new PackageVersionNotFound();
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

        /// <inheritdoc />
        public abstract Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true);

        /// <inheritdoc />
        public virtual Uri? GetPackageAbsoluteUri(PackageURL purl)
        {
            throw new NotImplementedException($"{GetType().Name} does not implement GetPackageAbsoluteUri.");
        }

        /// <inheritdoc />
        public virtual Task<PackageMetadata?> GetPackageMetadataAsync(PackageURL purl, bool includePrerelease = false, bool useCache = true)
        {
            string typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetPackageMetadata.");
        }

        /// <inheritdoc />
        public virtual JsonElement? GetVersionElement(JsonDocument contentJSON, Version version)
        {
            string typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetVersions.");
        }

        /// <inheritdoc />
        public virtual List<Version> GetVersions(JsonDocument? metadata)
        {
            string typeName = GetType().Name;
            throw new NotImplementedException($"{typeName} does not implement GetVersions.");
        }

        /// <inheritdoc />
        public async Task<Dictionary<PackageURL, double>> IdentifySourceRepositoryAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("IdentifySourceRepository({0})", purl);

            string rawMetadataString = await GetMetadataAsync(purl, useCache) ?? string.Empty;
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

        /// <inheritdoc />
        public virtual async Task<DateTime?> GetPublishedAtUtcAsync(PackageURL purl, bool useCache = true)
        {
            Check.NotNull(nameof(purl.Version), purl.Version);
            DateTime? uploadTime = (await GetPackageMetadataAsync(purl, useCache))?.UploadTime?.ToUniversalTime();
            return uploadTime;
        }

        /// <summary>
        /// Protected memory cache to make subsequent loads of the same URL fast and transparent.
        /// </summary>
        protected static readonly MemoryCache DataCache = new(
            new MemoryCacheOptions
            {
                SizeLimit = 1024 * 1024 * 100
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
