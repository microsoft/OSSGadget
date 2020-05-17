// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;

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
            { "download-directory", null },
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
                        bool.TryParse(downloadTool.Options["extract"]?.ToString(), out bool doExtract);
                        bool.TryParse(downloadTool.Options["download-metadata-only"]?.ToString(), out bool metadataOnly);
                        string targetDirectory = (string)downloadTool.Options["download-directory"];
                        // are we caching? 
                        bool doCaching = !string.IsNullOrEmpty(targetDirectory);

                        var purl = new PackageURL(target);
                        var packageDownloader = new PackageDownloader(purl, targetDirectory, doCaching);
                        foreach (var downloadPath in await packageDownloader.
                            DownloadPackageLocalCopy(purl, metadataOnly, doExtract))
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
                        if (!doCaching)
                        {
                            packageDownloader.ClearPackageLocalCopy();
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
