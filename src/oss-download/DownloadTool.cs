// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;
using NLog.Targets;

namespace Microsoft.CST.OpenSource
{
    public class DownloadTool
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-download";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(DownloadTool).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; }

        /// <summary>
        /// Command line options
        /// </summary>
        private readonly Dictionary<string, object> Options = new Dictionary<string, object>()
        {
            { "download-directory", "." },
            { "target", new List<string>() },
            { "extract", "true" },
            { "download-metadata-only", false}
        };

        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        static async Task Main(string[] args)
        {
            var downloadTool = new DownloadTool();
            Logger.Info($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");

            downloadTool.ParseOptions(args);

            if (((IList<string>)downloadTool.Options["target"]).Count > 0)
            {
                foreach (var target in (IList<string>)downloadTool.Options["target"])
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        foreach (var downloadPath in await downloadTool.EnsureDownloadExists(purl, (string)downloadTool.Options["download-directory"]))
                        {
                            if (string.IsNullOrEmpty(downloadPath))
                            {
                                Logger.Error("Unable to download {0}.", purl.ToString());
                            }
                            else
                            {
                                Logger.Info("Downloaded {0} to {1}", purl.ToString(), downloadPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                        Logger.Warn(ex.StackTrace);
                    }
                }
            }
            else
            {
                Logger.Warn("No target provided; nothing to download.");
                DownloadTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        public DownloadTool()
        {
            CommonInitialization.Initialize();
            Logger = CommonInitialization.Logger;
        }

        /// <summary>
        /// Check if the target folder has a directory in the name of the package. If it does not,
        /// download the package
        /// </summary>
        /// <param name="purl"></param>
        /// <param name="destinationDirectory"></param>
        /// <returns></returns>
        public async Task<List<string>> EnsureDownloadExists(PackageURL purl, string destinationDirectory = null)
        {
            if (purl == default)
            {
                Logger.Warn("Invalid PackageURL (null)");
                return new List<string>();
            }

            List<string> downloadDirectories = new List<string>();
            bool cached = !string.IsNullOrEmpty(destinationDirectory);
            destinationDirectory ??= ".";

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
                _downloader.TopLevelExtractionDirectory = destinationDirectory;
                var targetDirectory = _downloader.GetFullExtractionPath(purl);

                if (Directory.Exists(targetDirectory))
                {
                    // if the package directory exists in the target directory, 
                    // assume that the cache exists
                    downloadDirectories.Add(targetDirectory);
                    return downloadDirectories;
                }
                else
                {
                    Logger.Trace("Download({0})", purl?.ToString());
                    var directoryNames = await Download(_downloader, purl, cached);
                    downloadDirectories.AddRange(directoryNames);
                    return downloadDirectories;
                }
            }
            else
            {
                throw new ArgumentException(string.Format("Invalid Package URL type: {0}", purl?.Type));
            }
        }

        public async Task<List<string>> Download(BaseProjectManager _downloader, PackageURL purl, bool cached = false)
        {
            List<string> downloadPaths = new List<string>();
                if ((bool)Options["download-metadata-only"])
                {
                    var metadata = await _downloader.GetMetadata(purl);
                    if (metadata != default)
                    {
                        var outputFilename = Path.Combine(_downloader.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}");

                    if (!cached)
                    {
                        while (File.Exists(outputFilename))
                        {
                            outputFilename = Path.Combine(_downloader.TopLevelExtractionDirectory, $"metadata-{purl.ToStringFilename()}-{DateTime.Now.Ticks}");
                        }
                    }
                        File.WriteAllText(outputFilename, metadata);
                        downloadPaths.Add(outputFilename);
                    }
                }
                else
                {
                    if (!bool.TryParse(Options["extract"]?.ToString(), out bool doExtract))
                    {
                        doExtract = true;
                    }
                    downloadPaths = await _downloader.Download(purl, doExtract, cached);
                }
            
            return downloadPaths;
        }

        /// <summary>
        /// Parses options for this program.
        /// </summary>
        /// <param name="args">arguments (passed in from the user)</param>
        private void ParseOptions(string[] args)
        {
            if (args == null)
            {
                ShowUsage();
                Environment.Exit(1);
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-h":
                    case "--help":
                        ShowUsage();
                        Environment.Exit(1);
                        break;

                    case "-v":
                    case "--version":
                        Console.Error.WriteLine($"{TOOL_NAME} {VERSION}");
                        Environment.Exit(1);
                        break;
                    
                    case "--metadata":
                        Options["download-metadata-only"] = true;
                        break;
                    
                    case "--directory":
                        Options["download-directory"] = args[++i];
                        break;
                    
                    case "--no-extract":
                        Options["extract"] = "false";
                        break;

                    default:
                        ((IList<string>)Options["target"]).Add(args[i]);
                        break;
                }
            }
        }

        /// <summary>
        /// Displays usage information for the program.
        /// </summary>
        private static void ShowUsage()
        {
            Console.Error.WriteLine($@"
{TOOL_NAME} {VERSION}

Usage: {TOOL_NAME} [options] package-url...

positional arguments:
    package-url                 PackgeURL specifier to download (required, repeats OK)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --no-extract                  do not extract package contents 
  --metadata                    only download metadata, not package content
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
