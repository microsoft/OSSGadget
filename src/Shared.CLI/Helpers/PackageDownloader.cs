// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using Contracts;
    using Extensions;
    using Microsoft.CST.OpenSource.Helpers;
    using Microsoft.CST.OpenSource.PackageManagers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Class for managing the download of a single package.
    /// </summary>
    public class PackageDownloader
    {
        /// <summary>
        /// Constructor - creates a class object for downloading packages.
        /// </summary>
        /// <param name="purl">The package to download.</param>
        /// <param name="projectManagerFactory">The <see cref="ProjectManagerFactory"/> to use to get the project managers.</param>
        /// <param name="destinationDir">The directory where the package needs to be downloaded to.</param>
        /// <param name="doCaching">Check and use the cache if it exists - create if not.</param>
        public PackageDownloader(PackageURL purl, ProjectManagerFactory projectManagerFactory, string? destinationDir = null, bool doCaching = false)
        {
            if (purl == null)
            {
                throw new ArgumentNullException(nameof(purl), "PackageURL cannot be null");
            }

            doCache = doCaching;
            // if we are told to use caching, and it exists, believe that caching is still doable
            actualCaching = (doCaching && !string.IsNullOrEmpty(destinationDir) && Directory.Exists(destinationDir));

            // if no destination specified, dump the package in the temp directory
            if (string.IsNullOrEmpty(destinationDir))
            {
                usingTemp = true;
                destinationDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
            }
            else
            {
                destinationDirectory = destinationDir;
            }

            packageManager = projectManagerFactory.CreateProjectManager(purl, destinationDirectory);
            if (packageManager == null)
            {
                // Cannot continue without a package manager.
                throw new ArgumentException($"Invalid Package URL type: {purl.Type}", nameof(purl.Type));
            }
            PackageVersions = new List<PackageURL>();
            if (purl.Version == null || purl.Version.Equals("*"))
            {
                // figure out which version(s) we need to process
                PackageVersions = GetPackageVersionsToProcess(purl).Result;
            }
            else
            {
                PackageVersions.Add(purl);
            }
        }

        /// <summary>
        /// Deletes the destination directory for this package downloader if no destination directory was provided to <see cref="PackageDownloader(PackageURL, IHttpClientFactory?, string?, bool)"/>
        /// This can be used to clean up the temp folder that will be created when a path was not provided during creation.
        /// Note that the downloader will no longer work after calling this method.
        /// </summary>
        public void DeleteDestinationDirectoryIfTemp()
        {
            if (usingTemp)
            {
                FileSystemHelper.RetryDeleteDirectory(destinationDirectory);
            }
        }

        /// <summary>
        ///     Clears the cache directory
        /// </summary>
        public void ClearPackageLocalCopy()
        {
            try
            {
                foreach (string packageDirectory in downloadPaths)
                {
                    if (Directory.Exists(packageDirectory))
                    {
                        Logger.Trace("Removing directory {0}", packageDirectory);
                        FileSystemHelper.RetryDeleteDirectory(packageDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Trace("Error removing {0}: {1}", destinationDirectory, ex.Message);
            }

            downloadPaths.Clear();
        }

        /// <summary>
        ///     Clears the cache directory, if the cache argument was false, keep it for future processing otherwise
        /// </summary>
        public void ClearPackageLocalCopyIfNoCaching()
        {
            try
            {
                // if we were told to cache the copy by the caller, do not delete
                if (!doCache)
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
            List<string> downloadPaths = new();
            if (packageManager != null)
            {
                if (metadataOnly)
                {
                    string? metadata = await packageManager.GetMetadataAsync(purl);
                    if (metadata != null)
                    {
                        string outputFilename = Path.Combine(packageManager.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}");

                        // this will be effectively the same as above, if the cache doesnt exist
                        if (!actualCaching)
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
                    downloadPaths.AddRange(await packageManager.DownloadVersionAsync(purl, doExtract, actualCaching));
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

            List<string> downloadDirectories = new();
            foreach (PackageURL version in PackageVersions)
            {
                downloadDirectories.AddRange(await Download(version, metadataOnly, doExtract));
            }

            // Add the return values to our internal storage to be cleaned up later by CleanPackageLocalCopy
            downloadPaths.AddRange(downloadDirectories);

            return downloadDirectories;
        }

        /// <summary>
        ///     Get the package versions we need to process (if more than one indicated)
        /// </summary>
        /// <param name="purl"> </param>
        /// <returns> </returns>
        public async Task<List<PackageURL>> GetPackageVersionsToProcess(PackageURL purl)
        {
            List<PackageURL> packageVersions = new();

            if (packageManager != null)
            {
                // figure out which version we want to download
                PackageURL vPurl;
                if (purl.Version == null)
                {
                    try
                    {
                        IEnumerable<string>? versions = await packageManager.EnumerateVersionsAsync(purl, doCache);
                        if (versions.Any())
                        {
                            vPurl = new PackageURL(purl.Type, purl.Namespace, purl.Name, versions.First(), purl.Qualifiers, purl.Subpath);
                            packageVersions.Add(vPurl);
                        }
                        else
                        {
                            Logger.Warn("No versions were found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex, "Unable to enumerate versions: {0}", ex.Message);
                    }
                }
                else if (purl.Version.Equals("*"))
                {
                    try
                    {
                        foreach (string version in await packageManager.EnumerateVersionsAsync(purl, doCache))
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
                        Logger.Warn("Unable to enumerate versions, so cannot identify the latest: {0}", e.Message);
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
        private readonly bool actualCaching = false;

        // should we cache/check for the cache?
        private readonly bool doCache = false;
        private bool usingTemp;

        private string destinationDirectory { get; set; }

        private bool usingTempDir;

        // folders created
        private List<string> downloadPaths { get; set; } = new List<string>();

        private IBaseProjectManager? packageManager { get; set; }
        public List<PackageURL> PackageVersions { get; set; }
    }
}