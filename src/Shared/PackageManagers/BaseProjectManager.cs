// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using F23.StringSimilarity;
using AngleSharp.Dom;

namespace Microsoft.CST.OpenSource.Shared
{
    public class BaseProjectManager
    {
        /// <summary>
        /// Static HttpClient for use in all HTTP connections.
        /// </summary>
        protected static HttpClient WebClient;

        /// <summary>
        /// Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Protected memory cache to make subsequent loads of the same URL fast and transparent.
        /// </summary>
        protected static readonly MemoryCache DataCache = new MemoryCache(
            new MemoryCacheOptions
            {
                SizeLimit = 1024 * 1024 * 8
            }
        );

        /// <summary>
        /// Per-object option container.
        /// </summary>
        public Dictionary<string, object> Options { get; private set; }

        /// <summary>
        /// The location (directory) to extract files to.
        /// </summary>
        public string TopLevelExtractionDirectory { get; set; } = ".";

        
        public virtual Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            throw new NotImplementedException("BaseProjectManager does not implement EnumerateVersions.");
        }


        /// <summary>
        /// Downloads a given PackageURL and extracts it locally to a directory.
        /// </summary>
        /// <param name="purl">PackageURL to download</param>
        /// <returns>Paths (either files or directory names) pertaining to the downloaded files.</returns>
        public virtual Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract=true)
        {
            throw new NotImplementedException("BaseProjectManager does not implement DownloadVersion.");
        }

        /// <summary>
        /// This method should return text reflecting metadata for the given package.
        /// There is no assumed format.
        /// </summary>
        /// <param name="purl">PackageURL to search</param>
        /// <returns>a string containing metadata.</returns>
        public virtual Task<string> GetMetadata(PackageURL purl)
        {
            throw new NotImplementedException("BaseProjectManager does not implement GetMetadata.");
        }


        /// <summary>
        /// Implemented by all package managers to search the metadata, and either
        /// return a successful result for the package repository, or return a null 
        /// in case of failure/nothing to do.
        /// </summary>
        /// <returns></returns>
        protected virtual Task<Dictionary<PackageURL, double>> PackageMetadataSearch(PackageURL purl, string metadata)
        {
            throw new NotImplementedException("BaseProjectManager does not implement PackageMetadataSearch.");
        }

        /// <summary>
        /// Initializes a new project management object.
        /// </summary>
        public BaseProjectManager()
        {
            this.Options = new Dictionary<string, object>();
            CommonInitialization.OverrideEnvironmentVariables(this);
            WebClient = CommonInitialization.WebClient;
        }

        /// <summary>
        /// Retrieves JSON content from a given URI.
        /// </summary>
        /// <param name="uri">URI to load.</param>
        /// <returns>Content, as a JsonDocument, possibly from cache.</returns>
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

        /// <summary>
        /// Retrieves HTTP content from a given URI.
        /// </summary>
        /// <param name="uri">URI to load.</param>
        /// <returns></returns>
        public static async Task<string> GetHttpStringCache(string uri, bool useCache = true, bool neverThrow = false)
        {
            Logger.Trace("GetHttpStringCache({0}, {1})", uri, useCache);

            string resultString;
            
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
            } catch(Exception)
            {
                if (neverThrow)
                {
                    return default;
                }
                else
                {
                    throw;
                }
            }

            return resultString;
        }

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
            var purlList = new List<PackageURL>();

            // @TODO: Check the regex below; does this match GitHub's scheme?
            var githubRegex = new Regex(@"github\.com/([a-z0-9\-_\.]+)/([a-z0-9\-_\.]+)",
                                        RegexOptions.IgnoreCase);
            foreach (Match match in githubRegex.Matches(content))
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

        /// <summary>
        /// Extracts an archive (given by 'bytes') into a directory named
        /// 'directoryName', recursively, using MultiExtractor.
        /// </summary>
        /// <param name="directoryName"> directory to extract content into (within TopLevelExtractionDirectory)</param>
        /// <param name="bytes">bytes to extract (should be an archive file)</param>
        /// <returns></returns>
        public async Task<string> ExtractArchive(string directoryName, byte[] bytes)
        {
            Logger.Trace("ExtractArchive({0}, <bytes> len={1})", directoryName, bytes?.Length);

            Directory.CreateDirectory(TopLevelExtractionDirectory);

            // This will result in "npm-@types-foo@1.2.3" instead of "npm-%40types%2Ffoo@1.2.3"
            //directoryName = directoryName.Replace("%40", "@");
            //directoryName = directoryName.Replace("%2F", "-", StringComparison.InvariantCultureIgnoreCase);
            directoryName = directoryName.Replace(Path.DirectorySeparatorChar, '-');
            directoryName = directoryName.Replace(Path.AltDirectorySeparatorChar, '-');
            while (Directory.Exists(directoryName) || File.Exists(directoryName))
            {
                directoryName += "-" + DateTime.Now.Ticks;
            }
            var extractor = new Extractor();
            //extractor.MaxExtractedBytes = 1000 * 1000 * 10;  // 10 MB maximum package size

            foreach (var fileEntry in extractor.ExtractFile(directoryName, bytes))
            {
                var fullPath = fileEntry.FullPath.Replace(':', Path.DirectorySeparatorChar);

                // TODO: Does this prevent zip-slip?
                foreach (var c in Path.GetInvalidPathChars())
                {
                    fullPath = fullPath.Replace(c, '-');    // ignore: lgtm [cs/string-concatenation-in-loop] 
                }
                var filePathToWrite = Path.Combine(TopLevelExtractionDirectory, fullPath);
                filePathToWrite = filePathToWrite.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                Directory.CreateDirectory(Path.GetDirectoryName(filePathToWrite));
                await File.WriteAllBytesAsync(filePathToWrite, fileEntry.Content.ToArray());
            }

            var fullExtractionPath = Path.Combine(TopLevelExtractionDirectory, directoryName);
            fullExtractionPath = Path.GetFullPath(fullExtractionPath);
            Logger.Debug("Archive extracted to {0}", fullExtractionPath);

            return fullExtractionPath;
        }

        /// <summary>
        /// Downloads a given package, identified by 'purl', using
        /// the appropriate package manager.
        /// </summary>
        /// <param name="purl">package-url to download</param>
        /// <returns></returns>
        public async Task<List<string>> Download(PackageURL purl, bool doExtract = true)
        {
            Logger.Trace("(Base) Download({0})", purl?.ToString());
            var downloadPaths = new List<string>();

            if (purl == null)
            {
                return null;
            }
            else if (purl.Version == null)
            {
                var versions = await EnumerateVersions(purl);
                if (versions.Count() > 0)
                {
                    var vpurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, versions.Last(), purl.Qualifiers, purl.Subpath);
                    downloadPaths.AddRange(await DownloadVersion(vpurl, doExtract));
                }
                else
                {
                    Logger.Warn("Unable to enumerate versions, so cannot identify the latest.");
                }
            }
            else if (purl.Version.Equals("*"))
            {
                foreach (var version in await EnumerateVersions(purl))
                {
                    var vpurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, version, purl.Qualifiers, purl.Subpath);
                    downloadPaths.AddRange(await DownloadVersion(vpurl, doExtract));
                }
            }
            else
            {
                downloadPaths.AddRange(await DownloadVersion(purl, doExtract));
            }

            Logger.Debug("Downloaded to {0} paths", downloadPaths.Count);
            return downloadPaths;
        }

        /// <summary>
        /// Sort a collection of version strings, trying multiple ways.
        /// </summary>
        /// <param name="versionList">list of version strings</param>
        /// <returns>list of version strings, in sorted order</returns>
        public static IEnumerable<string> SortVersions(IEnumerable<string> versionList)
        {
            // Scrub the version list
            versionList = versionList.Select((v) =>
            {
                if (v.StartsWith("v", StringComparison.InvariantCultureIgnoreCase))
                {
                    return v.Substring(1).Trim();
                }
                else
                {
                    return v.Trim();
                }
            });

            // Attempt to sort using different methods
            List<Func<string, object>> methods = new List<Func<string, object>>
            {
                (s) => new Version(s),
                (s) => new SemVer.Version(s, loose: true),
                (s) => s
            };

            // Iterate through each method we defined above.
            foreach (var method in methods)
            {
                var objList = new List<object>();
                try
                {
                    foreach (var version in versionList)
                    {
                        objList.Add(method(version));
                    }
                    objList.Sort();  // Sort using the built-in sort, delegating to the type's comparator
                }
                catch (Exception)
                {
                    objList = null;
                }

                // If we have a successful result (right size), then we should be good.
                if (objList != null && objList.Count() == versionList.Count())
                {
                    return objList.Select(o => o.ToString());
                }
            }

            // Fallback, leaving it alone
            if (Logger.IsDebugEnabled)  // expensive string join, avoid unless necessary
            {
                Logger.Debug("List is not sortable, returning as-is: {0}", string.Join(", ", versionList));
            }
            return versionList;
        }

        /// <summary>
        /// Tries to find out the package repository from the metadata of the package.
        /// Check with the specific package manager, if they have any specific extraction 
        /// to do, w.r.t the metadata. If they found some package specific well defined metadata,
        /// use that.
        /// If that doesn't work, do a search across the metadata to find probable
        /// source repository urls
        /// </summary>
        /// <param name="purl">PackageURL to search</param>
        /// <returns>A dictionary, mapping each possible repo source entry to its probability/empty dictionary</returns>
        public async Task<Dictionary<PackageURL, double>> SearchMetadata(PackageURL purl)
        {
            var content = await GetMetadata(purl);
            // Check the specific PackageManager implementation.
            var candidates = await PackageMetadataSearch(purl, content);
            if (candidates != default && candidates.Any())
            {
                return candidates;
            }

            // if we reached here, we don't have any proper metadata
            // tagged for the source repository, so search for all
            // GitHub URLs and return all possible candidates.
            candidates = GetRepoCandidates(purl, content);

            // return a sort
            var sortedCandidates = from entry in candidates orderby entry.Value descending select entry;
            return sortedCandidates.ToDictionary(item => item.Key, item => item.Value);
        }

        /// <summary>
        /// Rank the source repo entry candidates by their edit distance.
        /// </summary>
        /// <param name="purl">the package</param>
        /// <param name="content">metadata of the package</param>
        /// <returns>Possible candidates of the package/empty dictionary</returns>
        protected Dictionary<PackageURL, double> GetRepoCandidates(PackageURL purl, string content)
        {
            var sourceUrls = GitHubProjectManager.ExtractGitHubUris(purl, content);
            var candidates = new Dictionary<PackageURL, double>();

            var uniqueItemsGroup = sourceUrls.GroupBy(item => item);
            if (sourceUrls != default && sourceUrls.Any())
            {
                // Since this is non-exact, we'll assign our confidence to 80%
                float baseScore = 0.8F;

                var l = new NormalizedLevenshtein();
                foreach (var group in uniqueItemsGroup)
                {
                    // the cumulative boosts should be < 0.2; otherwise it'd be an 1.0
                    // score by Levenshtein distance
                    double similarityBoost = l.Similarity(purl.Name, group.Key.Name) * 0.0001;
                    // give a similarly weighted boost based on the number of times a particular 
                    // candidate appear in the metadata
                    double countBoost = (double)(group.Count()) * 0.0001;
                    candidates.Add(group.Key,
                        baseScore +
                        similarityBoost +
                        countBoost);
                }
            }
            return candidates;
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
  pkg:github/foo/bar            TBD
  pkg:hackage/a50@*             All versions of a50 (via hackage.haskell.org)
  pkg:maven/org.apdplat/deep-qa The latest version of org.apdplat.deep-qa (via repo1.maven.org)
  pkg:npm/express               The latest version of Express (via npm.org)
  pkg:nuget/Newtonsoft.JSON     The latest version of Newtonsoft.JSON (via nuget.org)
  pkg:pypi/django@1.11.1        Version 1.11.1 fo Django (via pypi.org)
  pkg:ubuntu/zerofree           The latest version of zerofree from Ubuntu (via packages.ubuntu.com)
  pkg:vsm/MLNET/07              The latest version of MLNET.07 (from marketplace.visualstudio.com)
";
            return supportedHelpText;
        }
    }
}
