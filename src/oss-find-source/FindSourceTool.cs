// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class FindSourceTool
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-find-source";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(FindSourceTool).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; }

        /// <summary>
        /// Command line options
        /// </summary>
        private readonly Dictionary<string, object> Options = new Dictionary<string, object>()
        {
            { "target", new List<string>() },
            { "show-all", false },
            { "format", "text" },
            { "output-file", null }
        };

        static void Main(string[] args)
        {
            var findSourceTool = new FindSourceTool();
            Logger.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            findSourceTool.ParseOptions(args);

            bool redirectConsole = !string.IsNullOrEmpty((string)findSourceTool.Options["output-file"]);
            if(redirectConsole)
            {
                ConsoleHelper.RedirectConsole((string)findSourceTool.Options["output-file"]);
            }

            if (((IList<string>)findSourceTool.Options["target"]).Count > 0)
            {
                foreach (var target in (IList<string>)findSourceTool.Options["target"])
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        var results = findSourceTool.FindSource(purl).Result.ToList();
                        results.Sort((a, b) => (a.Value.CompareTo(b.Value)));
                        results.Reverse();

                        if((string)findSourceTool.Options["format"] == "text") {
                            PrintText(results);
                        }
                        else
                        {
                            PrintSarif(results);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger.Warn("No target provided; nothing to locate source for.");
                FindSourceTool.ShowUsage();
                Environment.Exit(1);
            }
            if (redirectConsole)
            {
                ConsoleHelper.RestoreConsole();
            }
        }

        public FindSourceTool()
        {
            CommonInitialization.Initialize();
            Logger = CommonInitialization.Logger;
        }

        static void PrintText(List<KeyValuePair<PackageURL, double>> results)
        {
            foreach (var result in results)
            {
                var confidence = result.Value * 100.0;
                Console.Out.WriteLine($"{confidence:0.0}%\thttps://github.com/{result.Key.Namespace}/{result.Key.Name} ({result.Key})");
            }
        }

        static void PrintSarif(List<KeyValuePair<PackageURL, double>> results)
        {
            List<Result> sarifResults = new List<Result>();
            foreach (var result in results)
            {
                Result sarifResult = new Result()
                {
                    Message = new Message()
                    {
                        Text = $"https://github.com/{result.Key.Namespace}/{result.Key.Name}"
                    }
                };

                var confidence = result.Value * 100.0;
                sarifResult.SetProperty("confidence", confidence);
                sarifResults.Add(sarifResult);
            }

            SarifBuilder sarifBuilder = new SarifBuilder();
            sarifBuilder.PrintSarifLog(sarifResults);
        }

        public async Task<Dictionary<PackageURL, double>> FindSource(PackageURL purl)
        {
            Logger.Trace("FindSource({0})", purl);

            var repositoryMap = new Dictionary<PackageURL, double>();
            
            if (purl == null)
            {
                Logger.Warn("FindSource was passed an invalid purl.");
                return repositoryMap;
            }

            var purlNoVersion = new PackageURL(purl.Type, purl.Namespace, purl.Name,
                                               null, purl.Qualifiers, purl.Subpath);
            Logger.Debug("Searching for source code for {0}", purlNoVersion.ToString());

            try
            {
                var repoSearcher = new RepoSearch();
                var repos = await repoSearcher.ResolvePackageLibraryAsync(purl);
                if (repos.Any())
                {
                    foreach (var key in repos.Keys)
                    {
                        repositoryMap[key] = repos[key];
                    }
                    Logger.Debug("Identified {0} repositories.", repos.Count);
                }
                else
                {
                    Logger.Warn("No repositories found for package {0}", purl);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error identifying source repository for {0}: {1}", purl, ex.Message);
            }

            return repositoryMap;
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

                    case "--format":
                        Options["format"] = args[++i];
                        break;

                    case "--output-file":
                        Options["output-file"] = args[++i];
                        break;

                    case "--show-all":
                        Options["show-all"] = true;
                        break;

                    case "-v":
                    case "--version":
                        Console.Error.WriteLine($"{TOOL_NAME} {VERSION}");
                        Environment.Exit(1);
                        break;
                    default:
                        ((IList<string>)Options["target"]).Add(args[i]);
                        break;
                }
            }

            if (((IList<string>)Options["target"]).Count == 0)
            {

                Logger.Error("Please enter the package(s) to search for");
                ShowUsage();
                Environment.Exit(1);
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
    package-url                 PackgeURL specifier to analyze (required, repeats OK)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --show-all                    show all possibilities of the package source repositories
                                 (default: show only the top result)
  --format                      selct the output format (text|sarif). (default is text)
  --output-file                 send the command output to a file instead of stdout
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
