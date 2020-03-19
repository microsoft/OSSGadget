// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.CST.OpenSource.Shared
{
    abstract public class BaseProjectManager
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
        /// Private memory cache to make subsequent loads of the same URL fast and transparent.
        /// </summary>
        private static readonly MemoryCache DataCache = new MemoryCache(
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

        
        abstract public Task<IEnumerable<string>> EnumerateVersions(PackageURL purl);

        /// <summary>
        /// Downloads a given PackageURL and extracts it locally to a directory.
        /// </summary>
        /// <param name="purl">PackageURL to download</param>
        /// <returns>Path to the directory containing the files extracted.</returns>
        abstract public Task<string> DownloadVersion(PackageURL purl, bool doExtract=true);

        /// <summary>
        /// This method should return text reflecting metadata for the given package.
        /// There is no assumed format.
        /// </summary>
        /// <param name="purl">PackageURL to search</param>
        /// <returns>a string containing metadata.</returns>
        abstract public Task<string> GetMetadata(PackageURL purl);

        /// <summary>
        /// Initializes a new project management object.
        /// </summary>
        protected BaseProjectManager()
        {
            this.Options = new Dictionary<string, object>();
            CommonInitialization.OverrideEnvironmentVariables(this);
            WebClient = CommonInitialization.WebClient;
        }

        ~BaseProjectManager()
        {
            DataCache.Dispose();
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
        public static async Task<string> GetHttpStringCache(string uri, bool useCache = true)
        {
            Logger.Trace("GetHttpStringCache({0}, {1})", uri, useCache);

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
            var resultString = await result.Content.ReadAsStringAsync();

            if (useCache)
            {
                lock (DataCache)
                {
                    var mce = new MemoryCacheEntryOptions() { Size = contentLength };
                    DataCache.Set<string>(uri, resultString, mce);
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
        /// <param name="directoryName"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        protected async Task<string> ExtractArchive(string directoryName, byte[] bytes)
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
            foreach (var fileEntry in extractor.ExtractFile(directoryName, bytes))
            {
                var fullPath = fileEntry.FullPath.Replace(':', Path.DirectorySeparatorChar);
                
                // TODO: Does this prevent zip-slip?
                foreach (var c in Path.GetInvalidPathChars())
                {
                    fullPath = fullPath.Replace(c, '-');
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

            if (purl.Version == null)
            {
                var versions = await EnumerateVersions(purl);
                var vpurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, versions.Last(), purl.Qualifiers, purl.Subpath);
                downloadPaths.Add(await DownloadVersion(vpurl, doExtract));
            }
            else if (purl.Version.Equals("*"))
            {
                foreach (var version in await EnumerateVersions(purl))
                {
                    var vpurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, version, purl.Qualifiers, purl.Subpath);
                    downloadPaths.Add(await DownloadVersion(vpurl, doExtract));
                }
            }
            else
            {
                downloadPaths.Add(await DownloadVersion(purl, doExtract));
            }
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
";
            return supportedHelpText;
        }
    }
}
