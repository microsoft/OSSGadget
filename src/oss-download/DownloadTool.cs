// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class DownloadTool : OSSGadget
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-download";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(DownloadTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>
        /// Command line options
        /// </summary>
        private readonly Dictionary<string, object> Options = new Dictionary<string, object>()
        {
            { "download-directory", "." },
            { "use-cache", false },
            { "target", new List<string>() },
            { "extract", true },
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
                        var downloadDirectory = (string)downloadTool.Options["download-directory"];
                        var useCache = (bool)downloadTool.Options["use-cache"];
                        var packageDownloader = new PackageDownloader(purl, downloadDirectory, useCache);

                        var downloadMetadataOnly = (bool)downloadTool.Options["download-metadata-only"];
                        var extract = (bool)downloadTool.Options["extract"];

                        var downloadResults = await packageDownloader.DownloadPackageLocalCopy(purl, downloadMetadataOnly, extract);
                        foreach (var downloadPath in downloadResults)
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
                            packageDownloader.ClearPackageLocalCopyIfNoCaching();
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

        public DownloadTool() : base()
        {
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
                    
                    case "--only-metadata":
                        Options["download-metadata-only"] = true;
                        break;
                    
                    case "--download-directory":
                        Options["download-directory"] = args[++i];
                        break;
                    
                    case "--no-extract":
                        Options["extract"] = false;
                        break;

                    case "--use-cache":
                        Options["use-cache"] = true;
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
  --only-metadata               only download metadata, not the package content
  --download-directory          the location to download the package to (default: current directory)
  --use-cache                   do not download the package if it is already present in the destination directory
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
