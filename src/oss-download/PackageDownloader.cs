// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;
using NLog.LayoutRenderers.Wrappers;
using NLog.Targets;

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

        bool doCache { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        public PackageDownloader(PackageURL purl, string destinationDirectory)
        {
            if (purl == null)
            {
                throw new ArgumentNullException("purl cannot be null");
            }

            this.destinationDirectory = destinationDirectory;
            // if we are told to use a cache dir, and it exists, believe that caching is still doable
            if (string.IsNullOrEmpty(destinationDirectory))
            {
                this.doCache = false;
                this.destinationDirectory = Directory.GetCurrentDirectory();
            }
            else
            {
                if (Directory.Exists(destinationDirectory))
                {
                    doCache = true;
                }
                else
                {
                    this.doCache = false;
                    Directory.CreateDirectory(destinationDirectory);
                }
            }

            this.packageManager = this.GetPackageManager(purl);

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
        /// Get the project manager for the package type
        /// </summary>
        /// <param name="purl"></param>
        /// <returns>BaseProjectManager object</returns>
        public BaseProjectManager GetPackageManager(PackageURL purl)
        {
            // Use reflection to find the correct package management class
            var downloaderClass = typeof(BaseProjectManager).Assembly.GetTypes()
               .Where(type => type.IsSubclassOf(typeof(BaseProjectManager)))
               .Where(type => type.Name.Equals($"{purl.Type}ProjectManager",
                                               StringComparison.InvariantCultureIgnoreCase))
               .FirstOrDefault();
            if (downloaderClass != null)
            {
                var ctor = downloaderClass.GetConstructor(Array.Empty<Type>());
                var _downloader = (BaseProjectManager)(ctor.Invoke(Array.Empty<object>()));
                return _downloader;
            }
            else { 
                throw new ArgumentException(string.Format("Invalid Package URL type: {0}", purl?.Type));
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
                downloadDirectories.AddRange(await this.DownloadPackage(version, metadataOnly, doExtract, this.doCache));
            }

            return downloadDirectories;
        }

        /// <summary>
        /// downloads (or returns existing path of) a package based on the cache flag.
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> DownloadPackage(
            PackageURL purl,
            bool metadataOnly,
            bool doExtract,
            bool doCache)
        {
            List<string> fullExtractionPathList = new List<string>();

            string targetDirectory = this.packageManager.GetFullExtractionPath(purl);
            if (doCache && Directory.Exists(targetDirectory))
            {
                fullExtractionPathList.Add(targetDirectory);
            }
            else
            {
                fullExtractionPathList.AddRange(
                    await Download(purl, metadataOnly, doExtract, doCache));
            }

            return fullExtractionPathList;
        }

        /// <summary>
        /// Clears the cache directory, if the cache argument was passed
        /// purl - the package for which the cache needs to be deleted
        /// destinationDirectory - the argument passed in by the user as the cache directory.
        /// If the destinationDirectory is not empty, it means the user passed in a cache option,
        /// and the directory wont be deleted.
        /// </summary>
        /// <param name="purl"></param>
        /// <param name="destinationDirectory"></param>
        public async void ClearPackageLocalCopy()
        {
            try
            {
                if (!this.doCache)
                {
                    foreach (PackageURL version in this.PackageVersions)
                    {
                        var packageDirectory = this.packageManager.GetFullExtractionPath(version);
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
            bool doExtract,
            bool cached = false)
        {
            List<string> downloadPaths = new List<string>();
            if (metadataOnly)
            {
                var metadata = await this.packageManager.GetMetadata(purl);
                if (metadata != default)
                {
                    var outputFilename = Path.Combine(this.packageManager.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}");

                    if (!cached)
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
                downloadPaths.AddRange(await this.packageManager.DownloadVersion(purl, doExtract, cached));
            }

            return downloadPaths;
        }
    }
}
