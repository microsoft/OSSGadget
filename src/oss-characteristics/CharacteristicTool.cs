﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.CST.OpenSource.Shared;
using NLog.Targets;

namespace Microsoft.CST.OpenSource
{
    public class CharacteristicTool
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-characteristic";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(CharacteristicTool).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; }

        /// <summary>
        /// Command line options
        /// </summary>
        public Dictionary<string, object> Options = new Dictionary<string, object>()
        {
            { "target", new List<string>() },
            { "disable-default-rules", false },
            { "custom-rule-directory", null },
            { "cache-directory", null },
        };

        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        static void Main(string[] args)
        {
            var characteristicTool = new CharacteristicTool();
            Logger.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            characteristicTool.ParseOptions(args);

            if (((IList<string>)characteristicTool.Options["target"]).Count > 0)
            {
                foreach (var target in (IList<string>)characteristicTool.Options["target"])
                {
                    try
                    {
                        string destinationDirectory = (string)characteristicTool.Options["cache-directory"] ?? ".";
                        var purl = new PackageURL(target);
                        var analysisResult = characteristicTool.AnalyzePackage(purl, destinationDirectory).Result;

                        var sb = new StringBuilder();
                        sb.AppendLine(target);
                        foreach (var key in analysisResult.Keys)
                        {
                            var metadata = analysisResult[key].Metadata;

                            
                            sb.AppendFormat("Programming Language: {0}\n", string.Join(", ", metadata.Languages.Keys));
                            sb.AppendLine("Unique Tags: ");
                            foreach (var tag in metadata.UniqueTags)
                            {
                                sb.AppendFormat($" * {tag}\n");
                            }
                        }

                        Logger.Info(sb.ToString());
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger.Warn("No target provided; nothing to analyze.");
                CharacteristicTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        public CharacteristicTool()
        {
            CommonInitialization.Initialize();
            Logger = CommonInitialization.Logger;
        }


        /// <summary>
        /// Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl">The package-url of the package to analyze.</param>
        /// <returns>List of tags identified</returns>
        public async Task<Dictionary<string, AnalyzeResult>> AnalyzePackage(PackageURL purl, string targetDirectoryName)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());
            
            var analysisResults = new Dictionary<string, AnalyzeResult>();
            List<string> directoryNames = new List<string>();

            var downloadTool = new DownloadTool();
            // ensure that the cache directory has the required package, download it otherwise
            if (!string.IsNullOrEmpty(targetDirectoryName))
            {
                directoryNames.AddRange(await downloadTool.EnsureDownloadExists(purl, targetDirectoryName));
            }
            if (directoryNames.Count > 0)
            { 
                foreach (var directoryName in directoryNames)
                {
                    var singleResult = await AnalyzeDirectory(directoryName);
                    analysisResults[directoryName] = singleResult;
                    Logger.Trace("Removing directory {0}", directoryName);
                    try
                    {
                        Directory.Delete(directoryName, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error removing {0}: {1}", directoryName, ex.Message);
                    }
                }
            }
            else
            {
                Logger.Warn("Error downloading {0}.", purl.ToString());
            }

            return analysisResults;
        }

        /// <summary>
        /// Analyzes a directory of files.
        /// </summary>
        /// <param name="directory">directory to analyze.</param>
        /// <returns>List of tags identified</returns>
        public async Task<AnalyzeResult> AnalyzeDirectory(string directory)
        {
            Logger.Trace("AnalyzeDirectory({0})", directory);

            AnalyzeResult analysisResult = default;

            // Call Application Inspector using the NuGet package
            var options = new AnalyzeOptions()
            {
                ConsoleVerbosityLevel = "None",
                LogFileLevel = "Off",
                SourcePath = directory,
                IgnoreDefaultRules = (bool)Options["disable-default-rules"],
                CustomRulesPath = (string)Options["custom-rule-directory"]
            };
            
            try
            {
                var analyzeCommand = new AnalyzeCommand(options);
                analysisResult = analyzeCommand.GetResult();
                Logger.Debug("Operation Complete: {0} files analyzed.", analysisResult?.Metadata?.TotalFiles);
            }
            catch(Exception ex)
            {
                Logger.Warn("Error analyzing {0}: {1}", directory, ex.Message);
            }

            return analysisResult;
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

                    case "--custom-rule-directory":
                        Options["custom-rule-directory"] = args[++i];
                        break;

                    case "--cache-directory":
                        Options["cache-directory"] = args[++i];
                        break;

                    case "--disable-default-rules":
                        Options["disable-default-rules"] = true;
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
  --custom-rule-directory DIR   load rules from directory DIR
  --disable-default-rules       do not load default, built-in rules.
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
