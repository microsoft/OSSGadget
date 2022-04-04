// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Microsoft.CST.OpenSource.Helpers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Utilities;

    internal class GitHubProjectManager : BaseProjectManager
    {
        public static string ENV_GITHUB_ENDPOINT = "https://github.com";

        public GitHubProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public GitHubProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Return all github repo patterns in the searchText.
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="searchText"> </param>
        /// <returns> </returns>
        public static IEnumerable<PackageURL> ExtractGitHubUris(PackageURL purl, string searchText)
        {
            List<PackageURL> repositoryList = new();
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
            GroupCollection matches = match.Groups;
            PackageURL packageURL = new(
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
            List<string> downloadedPaths = new();

            if (purl == null)
            {
                Logger.Debug("'purl' argument must not be null.");
                return downloadedPaths;
            }

            Logger.Trace("DownloadVersion {0}", purl.ToString());

            string? packageNamespace = purl?.Namespace;
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName)
                || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageNamespace, packageName);
                return downloadedPaths;
            }

            // Cut the .git off the end of the package name.
            if (packageName.EndsWith(".git"))
            {
                packageName = packageName[0..^4];
            }

            try
            {
                string url = $"{ENV_GITHUB_ENDPOINT}/{packageNamespace}/{packageName}";
                string fsNamespace = OssUtilities.NormalizeStringForFileSystem(packageNamespace);
                string fsName = OssUtilities.NormalizeStringForFileSystem(packageName);
                string fsVersion = OssUtilities.NormalizeStringForFileSystem(packageVersion);

                string relativeWorkingDirectory = string.IsNullOrWhiteSpace(packageVersion) ?
                                                $"github-{fsNamespace}-{fsName}" :
                                                $"github-{fsNamespace}-{fsName}-{fsVersion}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, relativeWorkingDirectory);

                if (doExtract && Directory.Exists(extractionPath) && cached == true)
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }

                // First, try a tag (most likely what we're looking for)
                List<string> archiveUrls = new();
                foreach (string prefix in new[] { "", "v" })
                {
                    archiveUrls.AddRange(new[] {
                        $"{url}/archive/refs/tags/{prefix}{packageVersion}.zip",
                        $"{url}/archive/{prefix}{packageVersion}.zip",
                        $"{url}/archive/refs/heads/{prefix}{packageVersion}.zip",
                    });
                }
                PackageURL purlNoVersion = new(purl!.Type, purl.Namespace, purl.Name, null, purl.Qualifiers, purl.Subpath);
                foreach (string v in EnumerateVersions(purlNoVersion).Result)
                {
                    if (Regex.IsMatch(purl.Version!, @"(^|[^\d\.])" + Regex.Escape(v)))
                    {
                        archiveUrls.Add($"{url}/archive/refs/tags/{v}.zip");
                    }
                }

                foreach (string archiveUrl in archiveUrls)
                {
                    Logger.Debug("Attemping to download {0}", archiveUrl);
                    HttpClient httpClient = CreateHttpClient();

                    System.Net.Http.HttpResponseMessage? result = await httpClient.GetAsync(archiveUrl);
                    if (result.IsSuccessStatusCode)
                    {
                        Logger.Debug("Download successful.");
                        if (doExtract)
                        {
                            downloadedPaths.Add(await ExtractArchiveAsync(TopLevelExtractionDirectory, relativeWorkingDirectory, await result.Content.ReadAsByteArrayAsync(), cached));
                        }
                        else
                        {
                            Directory.CreateDirectory(extractionPath);
                            string targetName = Path.Join(extractionPath, $"{fsVersion}.zip");
                            await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                            downloadedPaths.Add(targetName);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading GitHub package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            try
            {
                List<string> versionList = new();
                string githubUrl = $"https://github.com/{purl.Namespace}/{purl.Name}";

                ProcessStartInfo gitLsRemoteStartInfo = new()
                {
                    FileName = "git",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                gitLsRemoteStartInfo.ArgumentList.Add("ls-remote");
                gitLsRemoteStartInfo.ArgumentList.Add("--tags");
                gitLsRemoteStartInfo.ArgumentList.Add("--ref");
                gitLsRemoteStartInfo.ArgumentList.Add(githubUrl);

                Process? gitLsRemoteProcess = Process.Start(gitLsRemoteStartInfo);
                if (gitLsRemoteProcess != null)
                {
                    StreamReader? stdout = gitLsRemoteProcess.StandardOutput;
                    string? outputLine;
                    while ((outputLine = await gitLsRemoteProcess.StandardOutput.ReadLineAsync()) != null)
                    {
                        Match? match = Regex.Match(outputLine, "^.+refs/tags/(.*)$");
                        if (match.Success)
                        {
                            string? tagName = match.Groups[1].Value;
                            Logger.Debug("Adding tag: {0}", tagName);
                            versionList.Add(tagName);
                        }
                    }
                    string stderr = await gitLsRemoteProcess.StandardError.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        Logger.Warn("Error running 'git', error: {0}", stderr);
                    }
                }
                else
                {
                    Logger.Warn("Unable to run 'git'. Is it installed?");
                }
                return SortVersions(versionList);
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
        {
            await Task.CompletedTask;
            return $"https://github.com/{purl.Namespace}/{purl.Name}";
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_GITHUB_ENDPOINT}/{purl.Namespace}/{purl.Name}");
        }
        
        /// <summary>
        /// Gets if the URL is a GitHub repo URL.
        /// </summary>
        /// <param name="url">A URL to check if it's from GitHub.</param>
        /// <param name="purl">The variable to set the purl of if it is a GitHub repo.</param>
        /// <returns>If the url is a GitHubRepo.</returns>
        public static bool IsGitHubRepoUrl(string url, out PackageURL? purl)
        {
            Check.NotNull(nameof(url), url);
            purl = null;

            if (url.Contains(ENV_GITHUB_ENDPOINT))
            {
                try
                {
                    purl = ParseUri(new Uri(url));
                }
                catch (ArgumentException)
                {
                    return false;
                }

                return true;
            }
            return false;
        }

        private static readonly Regex GithubExtractorRegex = new(
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
        private static readonly Regex GithubMatchRegex = new(
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
