// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    /// <summary>
    /// Class for managing the download of a single package
    /// </summary>
    public class PackageDownloader
    {

        /// <summary>
        /// Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        List<PackageURL> PackageVersions { get; set; }

        BaseProjectManager packageManager { get; set; }

        string destinationDirectory { get; set; }

        // should we cache/check for the cache?
        bool doCache = false;

        // do we actually have a cache copy? or do we have to download?
        private bool actualCaching = false;

        // folders created
        List<string> downloadPaths { get; set; }

        /// <summary>
        /// Constuctor - creates a class object for downloading packages
        /// </summary>
        /// <param name="purl">package to download</param>
        /// <param name="destinationDir">the directory where the package needs to be placed</param>
        /// <param name="doCaching">check and use the cache if it exists - create if not</param>
        public PackageDownloader(PackageURL purl, string destinationDir = null, bool doCaching = false)
        {
            if (purl == null)
            {
                throw new ArgumentNullException("purl cannot be null");
            }

            this.doCache = doCaching;
            // if we are told to use caching, and it exists, believe that caching is still doable
            this.actualCaching = (doCaching && !string.IsNullOrEmpty(destinationDir) && Directory.Exists(destinationDir));

            // if no destination specified, dump the package in the temp directory
            this.destinationDirectory = string.IsNullOrEmpty(destinationDir) ? 
                Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()) : destinationDir;

            this.packageManager = ProjectManagerFactory.CreateProjectManager(purl, this.destinationDirectory);

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
        /// Get the package versions we need to process (if more than one indicated)
        /// </summary>
        /// <param name="purl"></param>
        /// <returns></returns>
        public async Task<List<PackageURL>> GetPackageVersionsToProcess(PackageURL purl)
        {
            List<PackageURL> packageVersions = new List<PackageURL>();

            if (this.packageManager != null)
            {
                // figure out which version we want to download
                PackageURL vPurl = default;
                if (purl.Version == null)
                {
                    var versions = await packageManager.EnumerateVersions(purl);
                    if (versions.Count() > 0)
                    {
                        Logger.Trace(string.Join(",", versions));
                        vPurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, versions.Last(), purl.Qualifiers, purl.Subpath);
                        packageVersions.Add(vPurl);

                    }
                    else
                    {
                        Logger.Warn("Unable to enumerate versions, so cannot identify the latest.");
                        // package list will remain empty
                    }
                }
                else if (purl.Version.Equals("*"))
                {
                    foreach (var version in await packageManager.EnumerateVersions(purl))
                    {
                        vPurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, version, purl.Qualifiers, purl.Subpath);
                        packageVersions.Add(vPurl);
                    }
                }
            }

            return packageVersions;
        }

        /// <summary>
        /// Check if the target folder has a directory in the name of the package. If it does not,
        /// download the package. This function handles both cached and non cached requests 
        /// for download and extract. if the target folder is specified, it assumes that caching 
        /// is requested; if not, it assumes there is no caching needed.
        /// </summary>
        /// <param name="purl">package to be downloaded</param>
        /// <param name="metadataOnly">whether to download only the package metadata, or the whole package</param>
        /// <param name="doExtract">Extract the package or not</param>
        /// <param name="destinationDirectory">the directory to use as cache for download</param>
        /// <returns></returns>
        public async Task<List<string>> DownloadPackageLocalCopy(PackageURL purl,
            bool metadataOnly,
            bool doExtract)
        {
            if (purl == default)
            {
                Logger.Warn("Invalid PackageURL (null)");
                return new List<string>();
            }

            List<string> downloadDirectories = new List<string>();
            foreach (var version in this.PackageVersions)
            {
                downloadDirectories.AddRange(await this.Download(version, metadataOnly, doExtract));
            }

            return downloadDirectories;
        }

        /// <summary>
        /// Clears the cache directory, if the cache argument was false, 
        /// keep it for future processing otherwise
        /// </summary>
        public async void ClearPackageLocalCopyIfNoCaching()
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
                Logger.Warn("Error removing {0}: {1}", destinationDirectory, ex.Message);
            }
        }

        /// <summary>
        /// Clears the cache directory
        /// </summary>
        public async void ClearPackageLocalCopy()
        {
            try
            {
                if (this.downloadPaths != null)
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
            }
            catch (Exception ex)
            {
                Logger.Warn("Error removing {0}: {1}", destinationDirectory, ex.Message);
            }
        }


        /// <summary>
        /// Downloads metadata if only metadata is requested; 
        /// downloads and extracts the package if doExtract is requested
        /// </summary>
        /// <param name="_downloader"></param>
        /// <param name="purl"></param>
        /// <param name="metadataOnly"></param>
        /// <param name="doExtract"></param>
        /// <param name="cached"></param>
        /// <returns>A list with 
        /// 1) the name of the file if metadata requested
        /// 2) The name of the file if package download and no extraction is requested
        /// 3) The directory of the downloaded and extracted package, if extraction is requested</returns>
        public async Task<List<string>> Download(
            PackageURL purl,
            bool metadataOnly,
            bool doExtract)
        {
            List<string> downloadPaths = new List<string>();
            if (metadataOnly)
            {
                var metadata = await this.packageManager.GetMetadata(purl);
                if (metadata != default)
                {
                    var outputFilename = Path.Combine(this.packageManager.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}");

                    // this will be effectively the same as above, if the cache doesnt exist
                    if (!this.actualCaching)
                    {
                        while (File.Exists(outputFilename))
                        {
                            outputFilename = Path.Combine(this.packageManager.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}-{DateTime.Now.Ticks}");
                        }
                    }
                    File.WriteAllText(outputFilename, metadata);
                    downloadPaths.Add(outputFilename);
                }
            }
            else
            {
                // only version download requests reach here
                downloadPaths.AddRange(await this.packageManager.DownloadVersion(purl, doExtract, this.actualCaching));
                this.downloadPaths = downloadPaths;
            }

            return downloadPaths;
        }
    }
}
