// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class GitHubProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_GITHUB_ENDPOINT = "https://github.com";

        public GitHubProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Return all github repo patterns in the searchText which have the same name as the package repo
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="searchText"> </param>
        /// <returns> </returns>
        public static IEnumerable<PackageURL> ExtractGitHubUris(PackageURL purl, string searchText)
        {
            List<PackageURL> repositoryList = new List<PackageURL>();
            if (string.IsNullOrEmpty(searchText))
            {
                return repositoryList;
            }

            try
            {
                foreach (Match match in GithubExtractorRegex.Matches(searchText).Where(match => match != null))
                {
                    repositoryList.Add(new PackageURL("github", match.Groups["user"].Value, match.Groups["repo"].Value, null, null, null));
                }
            }
            catch (UriFormatException ex)
            {
                Logger.Debug(ex, "Error matching regular expression: {0}", ex.Message);
                /* that was an invalid url, ignore */
            }
            return repositoryList;
        }

        public static PackageURL ParseUri(Uri uri)
        {
            Match match = GithubMatchRegex.Match(uri.AbsoluteUri);
            var matches = match.Groups;
            PackageURL packageURL = new PackageURL(
                "github",
                matches["namespace"].Value,
                matches["name"].Value,
                /* version doesnt make sense for source repo */ null,
                null,
                string.IsNullOrEmpty(matches["subpath"].Value) ? null : matches["subpath"].Value);
            return packageURL;
        }

        /// <summary>
        ///     Download one GitHub package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            var downloadedPaths = new List<string>();

            if (purl == null)
            {
                Logger.Debug("'purl' argument must not be null.");
                return downloadedPaths;
            }

            Logger.Trace("DownloadVersion {0}", purl.ToString());

            if (doExtract == false)
            {
                throw new NotImplementedException("GitHub does not support binary downloads yet.");
            }

            var packageNamespace = purl?.Namespace;
            var packageName = purl?.Name;
            var packageVersion = purl?.Version;

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName)
                || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageNamespace, packageName);
                return downloadedPaths;
            }

            try
            {
                var url = $"{ENV_GITHUB_ENDPOINT}/{packageNamespace}/{packageName}";
                var invalidChars = Path.GetInvalidFileNameChars();

                // TODO: Externalize this normalization
                var fsNamespace = new string((packageNamespace.Select(ch => invalidChars.Contains(ch) ? '_' : ch) ?? Array.Empty<char>()).ToArray());
                var fsName = new string((packageName.Select(ch => invalidChars.Contains(ch) ? '_' : ch) ?? Array.Empty<char>()).ToArray());
                var fsVersion = new string((packageVersion.Select(ch => invalidChars.Contains(ch) ? '_' : ch) ?? Array.Empty<char>()).ToArray());
                var workingDirectory = string.IsNullOrWhiteSpace(packageVersion) ?
                                        Path.Join(TopLevelExtractionDirectory, $"github-{fsNamespace}-{fsName}") :
                                        Path.Join(TopLevelExtractionDirectory, $"github-{fsNamespace}-{fsName}-{fsVersion}");
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, workingDirectory);
                if (doExtract && Directory.Exists(extractionPath) && cached == true)
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }

                Repository.Clone(url, workingDirectory);

                var repo = new Repository(workingDirectory);
                Commands.Checkout(repo, packageVersion);
                downloadedPaths.Add(workingDirectory);
                repo.Dispose();
            }
            catch (LibGit2Sharp.NotFoundException ex)
            {
                Logger.Debug(ex, "The version {0} is not a valid git reference: {1}", packageVersion, ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading GitHub package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            try
            {
                var versionList = new List<string>();
                var githubUrl = $"https://github.com/{purl.Namespace}/{purl.Name}";
                // TODO: Document why we're wrapping this in a task
                await Task.Run(() =>
                {
                    foreach (var reference in Repository.ListRemoteReferences(githubUrl))
                    {
                        if (reference.IsTag)
                        {
                            var tagName = reference.ToString().Replace("refs/tags/", "");
                            versionList.Add(tagName);
                        }
                    }
                });
                versionList.Sort();
                return versionList.Select(v => v.ToString());
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error enumerating GitHub repository references: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            await Task.Run(() => { });  // Avoid async warning -- @HACK
            return $"https://github.com/{purl.Namespace}/{purl.Name}";
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_GITHUB_ENDPOINT}/{purl.Namespace}/{purl.Name}");
        }

        private static readonly Regex GithubExtractorRegex = new Regex(
                    @"((?<protocol>https?|git|ssh|rsync)\+?)+\://" +
                    @"(?:(?<username>[\w-]+)@)*" +
                    @"(github\.com)" +
                    @"[:/]*" +
                    @"(?<port>[\d]+)?" +
                    @"/(?<user>[\w-\.]+)" +
                    @"/(?<repo>[\w-\.]+)/?",
                        RegexOptions.Compiled);

        /// <summary>
        ///     Regular expression that matches possible GitHub URLs
        /// </summary>
        private static readonly Regex GithubMatchRegex = new Regex(
            @"^((?<protocol>https?|git|ssh|rsync)\+?)+\://" +
            @"(?:(?<user>.+)@)*" +
            @"(?<resource>[a-z0-9_.-]*)" +
            @"[:/]*" +
            @"(?<port>[\d]+)?" +
            @"(?<pathname>\/((?<namespace>[\w\-\.]+)/)" +
            @"(?<subpath>[\w\-]+/)*" +
            @"((?<name>[\w\-\.]+?)(\.git|/)?)?)$",
                RegexOptions.Singleline | RegexOptions.Compiled);
    }
}