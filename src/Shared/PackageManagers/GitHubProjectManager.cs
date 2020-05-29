// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace Microsoft.CST.OpenSource.Shared
{
    class GitHubProjectManager : BaseProjectManager
    {
        /// <summary>
        /// Regular expression that matches possible GitHub URLs
        /// </summary>
        static readonly Regex GithubMatchRegex = new Regex(
            @"^((?<protocol>https?|git|ssh|rsync)\+?)+\://" +
            @"(?:(?<user>.+)@)*" +
            @"(?<resource>[a-z0-9_.-]*)" +
            @"[:/]*" +
            @"(?<port>[\d]+){0,1}" +
            @"(?<pathname>\/((?<namespace>[\w\-]+)\/)" +
            @"(?<subpath>[\w\-]+\/)*" +
            @"((?<name>[\w\-\.]+?)(\.git|\/)?)?)$",
                RegexOptions.Singleline | RegexOptions.Compiled);


        static readonly Regex GithubExtractorRegex = new Regex(
            @"((?<protocol>https?|git|ssh|rsync)\+?)+\://" +
            @"(?:(?<username>[\w-]+)@)*" +
            @"(github\.com)" +
            @"[:/]*" +
            @"(?<port>[\d]+){0,1}" +
            @"\/(?<user>[\w-]+)" +
            @"\/(?<repo>[\w-]+)\/?",
                RegexOptions.Compiled);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_GITHUB_ENDPOINT = "https://github.com";

        public GitHubProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        /// Download one GitHub package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            if (doExtract == false)
            {
                throw new NotImplementedException("GitHub does not support binary downloads yet.");
            }

            var packageNamespace = purl?.Namespace;
            var packageName = purl?.Name;
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName))
            {
                Logger.Error("Unable to download [{0} {1}]. Both must be defined.", packageNamespace, packageName);
                return downloadedPaths;
            }

            try
            {
                var url = $"{ENV_GITHUB_ENDPOINT}/{purl.Namespace}/{purl.Name}";
                var invalidChars = Path.GetInvalidFileNameChars();

                // TODO: Externalize this normalization
                var fsNamespace = new String(purl.Namespace.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var fsName = new String(purl.Name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var fsVersion = new String(purl.Version.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var workingDirectory = string.IsNullOrWhiteSpace(purl.Version) ?
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
                if (!string.IsNullOrWhiteSpace(purl.Version))
                {
                    Commands.Checkout(repo, purl.Version);
                    downloadedPaths.Add(workingDirectory);
                }
                repo.Dispose();
            }
            catch (LibGit2Sharp.NotFoundException ex)
            {
                Logger.Warn(ex, "The version {0} is not a valid git reference: {1}", purl.Version, ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error downloading GitHub package: {0}", ex.Message);
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
                Logger.Error(ex, $"Error enumerating GitHub repository references: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        public override async Task<string> GetMetadata(PackageURL purl)
        {
            await Task.Run(() => { });  // Avoid async warning -- @HACK
            return $"https://github.com/{purl.Namespace}/{purl.Name}";
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
        /// Return all github repo patterns in the searchText which have the same name as the package repo
        /// </summary>
        /// <param name="purl"></param>
        /// <param name="searchText"></param>
        /// <returns></returns>
        public static IEnumerable<PackageURL> ExtractGitHubUris(PackageURL purl, string searchText)
        {
            List<PackageURL> repositoryList = new List<PackageURL>();
            if (string.IsNullOrEmpty(searchText))
            {
                return repositoryList;
            }

            try
            {
                foreach (Match match in GithubExtractorRegex.Matches(searchText))
                {
                    repositoryList.Add(new PackageURL("github", match.Groups["user"].Value, match.Groups["repo"].Value, null, null, null));
                }
            }
            catch (UriFormatException ex)
            {
                Logger.Warn(ex, "Error matching regular expression: {0}", ex.Message);
                /* that was an invalid url, ignore */
            }
            return repositoryList;
        }
    }
}
