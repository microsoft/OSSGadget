// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace Microsoft.OpenSource.Shared
{
    class GitHubProjectManager : BaseProjectManager
    {
        /// <summary>
        /// Download one GitHub package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<string> DownloadVersion(PackageURL purl)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            string downloadedPath = null;

            if (string.IsNullOrWhiteSpace(packageName))
            {
                Logger.Error("Unable to download [{0}]", packageName);
                return null;
            }

            try
            {
                var url = $"https://github.com/{purl.Namespace}/{purl.Name}";
                var invalidChars = Path.GetInvalidFileNameChars();

                var fsNamespace = new String(purl.Namespace.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var fsName = new String(purl.Name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var fsVersion = new String(purl.Version.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var workingDirectory = string.IsNullOrWhiteSpace(purl.Version) ?
                                        Path.Join(TopLevelExtractionDirectory, $"github-{fsNamespace}-{fsName}") :
                                        Path.Join(TopLevelExtractionDirectory, $"github-{fsNamespace}-{fsName}-{fsVersion}");

                Repository.Clone(url, workingDirectory);
                using var repo = new Repository(workingDirectory);

                if (!string.IsNullOrWhiteSpace(purl.Version))
                {
                    Commands.Checkout(repo, purl.Version);
                    downloadedPath = workingDirectory;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error downloading GitHub package: {0}", ex.Message);
                downloadedPath = null;
            }
            return downloadedPath;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl)
        {
            try
            {
                var versionList = new List<string>();
                var url = $"https://github.com/{purl.Namespace}/{purl.Name}";
                await Task.Run(() =>
                {
                    foreach (var reference in Repository.ListRemoteReferences(url))
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
    }
}
