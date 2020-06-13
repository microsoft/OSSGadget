// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource
{
    public class CharacteristicTool : OSSGadget
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-characteristic";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(CharacteristicTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>
        /// Command line options
        /// </summary>
        public Dictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "disable-default-rules", false },
            { "custom-rule-directory", null },
            { "download-directory", null },
            { "use-cache", false },
            { "format", "text" },
            { "output-file", null }
        };

        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        static void Main(string[] args)
        {
            var characteristicTool = new CharacteristicTool();
            Logger?.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            characteristicTool.ParseOptions(args);

            // output to console or file?
            bool redirectConsole = !string.IsNullOrEmpty((string?)characteristicTool.Options["output-file"]);
            if (redirectConsole)
            {
                if (!ConsoleHelper.RedirectConsole((string?)characteristicTool.Options["output-file"] ?? 
                    string.Empty))
                {
                    Logger?.Error("Could not switch output from console to file");
                    // continue with current output
                }
            }

            // select output format
            OutputBuilder? outputBuilder;
            try
            {
                outputBuilder = new OutputBuilder((string?)characteristicTool.Options["format"] ??
                                        OutputBuilder.OutputFormat.text.ToString());
            }
            catch (ArgumentOutOfRangeException)
            {
                Logger?.Error("Invalid output format");
                return;
            }

            if (characteristicTool.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        var analysisResult = characteristicTool.AnalyzePackage(purl, 
                            (string?)characteristicTool.Options["download-directory"], 
                            (bool?)characteristicTool.Options["use-cache"] == true).Result;

                        characteristicTool.AppendOutput(outputBuilder, purl, analysisResult);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            } 
            else
            {
                Logger?.Warn("No target provided; nothing to analyze.");
                CharacteristicTool.ShowUsage();
                Environment.Exit(1);
            }
            if (redirectConsole)
            {
                ConsoleHelper.RestoreConsole();
            }
        }

        public CharacteristicTool() : base()
        {
        }


        /// <summary>
        /// Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl">The package-url of the package to analyze.</param>
        /// <returns>List of tags identified</returns>
        public async Task<Dictionary<string, AnalyzeResult?>> AnalyzePackage(PackageURL purl, 
            string? targetDirectoryName, 
            bool doCaching)
        {
            Logger?.Trace("AnalyzePackage({0})", purl.ToString());

            var analysisResults = new Dictionary<string, AnalyzeResult?>();

            var packageDownloader = new PackageDownloader(purl, targetDirectoryName, doCaching);
            // ensure that the cache directory has the required package, download it otherwise
            var directoryNames = await packageDownloader.DownloadPackageLocalCopy(purl, 
                false, 
                true);
            if (directoryNames.Count > 0)
            {
                foreach (var directoryName in directoryNames)
                {
                    var singleResult = await AnalyzeDirectory(directoryName);
                    analysisResults[directoryName] = singleResult;
                }
            }
            else
            {
                Logger?.Warn("Error downloading {0}.", purl.ToString());
            }
            packageDownloader.ClearPackageLocalCopyIfNoCaching();
            return analysisResults;
        }

        /// <summary>
        /// Analyzes a directory of files.
        /// </summary>
        /// <param name="directory">directory to analyze.</param>
        /// <returns>List of tags identified</returns>
        public async Task<AnalyzeResult?> AnalyzeDirectory(string directory)
        {
            Logger?.Trace("AnalyzeDirectory({0})", directory);

            AnalyzeResult? analysisResult = null;

            // Call Application Inspector using the NuGet package
            var options = new AnalyzeOptions()
            {
                ConsoleVerbosityLevel = "None",
                LogFileLevel = "Off",
                SourcePath = directory,
                IgnoreDefaultRules = (bool?)Options["disable-default-rules"] == true,
                CustomRulesPath = (string?)Options["custom-rule-directory"],
            };
            
            try
            {
                var analyzeCommand = new AnalyzeCommand(options);
                analysisResult = analyzeCommand.GetResult();
                Logger?.Debug("Operation Complete: {0} files analyzed.", analysisResult?.Metadata?.TotalFiles);
            }
            catch(Exception ex)
            {
                Logger?.Warn("Error analyzing {0}: {1}", directory, ex.Message);
            }

            return analysisResult;
        }

        /// <summary>
        /// Convert charactersticTool results into output format
        /// </summary>
        /// <param name="outputBuilder"></param>
        /// <param name="purl"></param>
        /// <param name="results"></param>
        void AppendOutput(OutputBuilder outputBuilder, PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResults)
        {
            if (outputBuilder.isTextFormat())
            {
                outputBuilder.AppendOutput(GetTextResults(purl, analysisResults));
            }
            else
            {
                outputBuilder.AppendOutput(GetSarifResults(purl, analysisResults));
            }
        }

        /// <summary>
        /// Convert charactersticTool results into text format
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        static string GetTextResults(PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResult)
        {
            StringBuilder stringOutput = new StringBuilder();
            stringOutput.AppendLine(purl.ToString());
            if (analysisResult.HasAtLeastOneNonNullValue())
            {
                foreach (var key in analysisResult.Keys)
                {
                    var metadata = analysisResult?[key]?.Metadata;

                    stringOutput.AppendFormat("Programming Language: {0}\n", 
                        string.Join(", ", metadata?.Languages?.Keys ?? Array.Empty<string>().ToList()));
                    stringOutput.AppendLine("Unique Tags: ");
                    foreach (var tag in metadata?.UniqueTags ?? new ConcurrentDictionary<string, byte>())
                    {
                        stringOutput.AppendFormat($" * {tag}\n");
                    }
                }
            }
            return stringOutput.ToString();
        }

        /// <summary>
        /// Build and return a list of Sarif Result list from the find characterstics results
        /// </summary>
        /// <param name="purl"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        static List<SarifResult> GetSarifResults(PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResult)
        {
            List<SarifResult> sarifResults = new List<SarifResult>();
            if (analysisResult.HasAtLeastOneNonNullValue())
            {
                foreach (var key in analysisResult.Keys)
                {
                    var metadata = analysisResult?[key]?.Metadata;
                    SarifResult sarifResult = new SarifResult()
                    {
                        Message = new Message()
                        {
                            Text = string.Join(", ", metadata?.Languages?.Keys ?? Array.Empty<string>()),
                            Id = "languages"
                        },
                        Kind = ResultKind.Informational,
                        Level = FailureLevel.None,
                        Locations = OutputBuilder.BuildPurlLocation(purl),
                    };

                    if (metadata?.UniqueTags?.HasAtLeastOneNonNullValue() ?? true)
                    {
                        foreach (var tag in metadata?.UniqueTags ?? new ConcurrentDictionary<string, byte>())
                        {
                            sarifResult?.SetProperty(tag.Key, tag.Value);
                        }
                    }
                    sarifResults.Add(sarifResult);
                }
            }
            return sarifResults;
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

                    case "--download-directory":
                        Options["download-directory"] = args[++i];
                        break;

                    case "--use-cache":
                        Options["use-cache"] = true;
                        break;

                    case "--disable-default-rules":
                        Options["disable-default-rules"] = true;
                        break;

                    case "--format":
                        Options["format"] = args[++i];
                        break;

                    case "--output-file":
                        Options["output-file"] = args[++i];
                        break;

                    default:
                        if (Options["target"] is IList<string> targetList)
                            targetList.Add(args[i]);
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
  --download-directory          the directory to download the package to
  --use-cache                   do not download the package if it is already present in the destination directory
  --format                      selct the output format (text|sarifv1|sarifv2). (default is text)
  --output-file                 send the command output to a file instead of stdout
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
