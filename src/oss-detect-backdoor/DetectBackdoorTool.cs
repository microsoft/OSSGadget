// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class DetectBackdoorTool
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-detect-backdoor";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(DetectBackdoorTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>
        /// Location of the backdoor detection rules.
        /// </summary>
        private const string RULE_DIRECTORY = @"Resources\BackdoorRules";

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Command line options
        /// </summary>
        private readonly Dictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "download-directory", null },
            { "use-cache", false }
        };

        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        static async Task Main(string[] args)
        {
            var detectBackdoorTool = new DetectBackdoorTool();
            Logger?.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            detectBackdoorTool.ParseOptions(args);

            if (detectBackdoorTool.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                var characteristicTool = new CharacteristicTool();
                characteristicTool.Options["target"] = detectBackdoorTool.Options["target"];
                characteristicTool.Options["disable-default-rules"] = true;
                characteristicTool.Options["custom-rule-directory"] = RULE_DIRECTORY;
                characteristicTool.Options["download-directory"] = detectBackdoorTool.Options["download-directory"];
                characteristicTool.Options["use-cache"] = detectBackdoorTool.Options["use-cache"];

                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        characteristicTool.AnalyzePackage(purl, 
                            (string?)detectBackdoorTool.Options["download-directory"], 
                            (bool?)detectBackdoorTool.Options["use-cache"] == true).Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger?.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger?.Warn("No target provided; nothing to analyze.");
                DetectBackdoorTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        public DetectBackdoorTool() : base()
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

                    case "--download-directory":
                        Options["download-directory"] = args[++i];
                        break;

                    case "--use-cache":
                        Options["use-cache"] = true;
                        break;

                    default:
                        if (Options["target"] is IList<string> targetList)
                        {
                            targetList.Add(args[i]);
                        }
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
  --download-directory          the directory to download the package to
  --use-cache                   do not download the package if it is already present in the destination directory
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
