// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    /// <summary>
    ///     Class for managing the download of a single package
    /// </summary>
    public class PackageDownloader
    {
        /// <summary>
        ///     Constuctor - creates a class object for downloading packages
        /// </summary>
        /// <param name="purl"> package to download </param>
        /// <param name="destinationDir"> the directory where the package needs to be placed </param>
        /// <param name="doCaching"> check and use the cache if it exists - create if not </param>
        public PackageDownloader(PackageURL? purl, string? destinationDir = null, bool doCaching = false)
        {
            if (purl == null)
            {
                throw new ArgumentNullException("PackageURL cannot be null");
            }

            this.doCache = doCaching;
            // if we are told to use caching, and it exists, believe that caching is still doable
            this.actualCaching = (doCaching && !string.IsNullOrEmpty(destinationDir) && Directory.Exists(destinationDir));

            // if no destination specified, dump the package in the temp directory
            this.destinationDirectory = string.IsNullOrEmpty(destinationDir) ?
                Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()) : destinationDir;

            this.packageManager = ProjectManagerFactory.CreateProjectManager(purl, this.destinationDirectory);
            if (this.packageManager == null)
            {
                // Cannot continue without package manager
                throw new ArgumentException("Invalid Package URL type: {0}", purl.Type);
            }
            this.PackageVersions = new List<PackageURL>();
            if (purl.Version == null || purl.Version.Equals("*"))
            {
                // figure out which version(s) we need to process
                this.PackageVersions = this.GetPackageVersionsToProcess(purl).Result;
            }
            else
            {
                this.PackageVersions.Add(purl);
            }
        }

        /// <summary>
        ///     Clears the cache directory
        /// </summary>
        public void ClearPackageLocalCopy()
        {
            try
            {
                foreach (string packageDirectory in this.downloadPaths)
                {
                    if (Directory.Exists(packageDirectory))
                    {
                        Logger.Trace("Removing directory {0}", packageDirectory);
                        Directory.Delete(packageDirectory, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Trace("Error removing {0}: {1}", destinationDirectory, ex.Message);
            }

            this.downloadPaths.Clear();
        }

        /// <summary>
        ///     Clears the cache directory, if the cache argument was false, keep it for future processing otherwise
        /// </summary>
        public void ClearPackageLocalCopyIfNoCaching()
        {
            try
            {
                // if we were told to cache the copy by the caller, do not delete
                if (!this.doCache)
                {
                    ClearPackageLocalCopy();
                }
            }
            catch (Exception ex)
            {
                Logger.Trace("Error removing {0}: {1}", destinationDirectory, ex.Message);
            }
        }

        /// <summary>
        ///     Downloads metadata if only metadata is requested; downloads and extracts the package if
        ///     doExtract is requested
        /// </summary>
        /// <param name="_downloader"> </param>
        /// <param name="purl"> </param>
        /// <param name="metadataOnly"> </param>
        /// <param name="doExtract"> </param>
        /// <param name="cached"> </param>
        /// <returns>
        ///     A list with
        ///     1) the name of the file if metadata requested
        ///     2) The name of the file if package download and no extraction is requested
        ///     3) The directory of the downloaded and extracted package, if extraction is requested
        /// </returns>
        public async Task<List<string>> Download(
            PackageURL purl,
            bool metadataOnly,
            bool doExtract)
        {
            List<string> downloadPaths = new List<string>();
            if (packageManager != null)
            {
                if (metadataOnly)
                {
                    var metadata = await packageManager.GetMetadata(purl);
                    if (metadata != null)
                    {
                        var outputFilename = Path.Combine(packageManager.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}");

                        // this will be effectively the same as above, if the cache doesnt exist
                        if (!this.actualCaching)
                        {
                            while (File.Exists(outputFilename))
                            {
                                outputFilename = Path.Combine(packageManager.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}-{DateTime.Now.Ticks}");
                            }
                        }
                        File.WriteAllText(outputFilename, metadata);
                        downloadPaths.Add(outputFilename);
                    }
                }
                else
                {
                    // only version download requests reach here
                    downloadPaths.AddRange(await packageManager.DownloadVersion(purl, doExtract, this.actualCaching));
                }
            }

            // Add the return values to our internal storage to be cleaned up later by CleanPackageLocalCopy
            this.downloadPaths.AddRange(downloadPaths);

            return downloadPaths;
        }

        /// <summary>
        ///     Check if the target folder has a directory in the name of the package. If it does not,
        ///     download the package. This function handles both cached and non cached requests for download
        ///     and extract. if the target folder is specified, it assumes that caching is requested; if not,
        ///     it assumes there is no caching needed.
        /// </summary>
        /// <param name="purl"> package to be downloaded </param>
        /// <param name="metadataOnly">
        ///     whether to download only the package metadata, or the whole package
        /// </param>
        /// <param name="doExtract"> Extract the package or not </param>
        /// <param name="destinationDirectory"> the directory to use as cache for download </param>
        /// <returns> </returns>
        public async Task<List<string>> DownloadPackageLocalCopy(PackageURL purl,
            bool metadataOnly,
            bool doExtract)
        {
            if (purl == null)
            {
                Logger.Debug("Invalid PackageURL (null)");
                return new List<string>();
            }

            List<string> downloadDirectories = new List<string>();
            foreach (var version in this.PackageVersions)
            {
                downloadDirectories.AddRange(await this.Download(version, metadataOnly, doExtract));
            }

            // Add the return values to our internal storage to be cleaned up later by CleanPackageLocalCopy
            this.downloadPaths.AddRange(downloadDirectories);

            return downloadDirectories;
        }

        /// <summary>
        ///     Get the package versions we need to process (if more than one indicated)
        /// </summary>
        /// <param name="purl"> </param>
        /// <returns> </returns>
        public async Task<List<PackageURL>> GetPackageVersionsToProcess(PackageURL purl)
        {
            List<PackageURL> packageVersions = new List<PackageURL>();

            if (this.packageManager != null)
            {
                // figure out which version we want to download
                PackageURL vPurl;
                if (purl.Version == null)
                {
                    try
                    {
                        var versions = await packageManager.EnumerateVersions(purl);
                        if (versions.Any())
                        {
                            var versionList = versions.Select(s => VersionComparer.Parse(s)).ToList();
                            versionList.Sort(new VersionComparer());
                            var latestVersion = string.Join("", versionList.First());
                            vPurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, latestVersion, purl.Qualifiers, purl.Subpath);
                            packageVersions.Add(vPurl);
                        }
                        else
                        {
                            throw new InvalidDataException("No versions were returned from EnumerateVersions.");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e, "Unable to enumerate versions, so cannot identify the latest. {0}", e.Message);
                        // package list will remain empty
                    }
                }
                else if (purl.Version.Equals("*"))
                {
                    try
                    {

                        foreach (var version in await packageManager.EnumerateVersions(purl))
                        {
                            vPurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, version, purl.Qualifiers, purl.Subpath);
                            packageVersions.Add(vPurl);
                        }
                        if (!packageVersions.Any())
                        {
                            throw new InvalidDataException("No versions were returned from EnumerateVersions.");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Debug($"Unable to enumerate versions, so cannot identify the latest. {e.Message}:{e.StackTrace}");
                        // package list will remain empty
                    }
                }
            }

            return packageVersions;
        }

        /// <summary>
        ///     Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        // do we actually have a cache copy? or do we have to download?
        private bool actualCaching = false;

        // should we cache/check for the cache?
        private bool doCache = false;

        private string destinationDirectory { get; set; }

        // folders created
        private List<string> downloadPaths { get; set; } = new List<string>();

        private BaseProjectManager? packageManager { get; set; }
        private List<PackageURL> PackageVersions { get; set; }
    }
}