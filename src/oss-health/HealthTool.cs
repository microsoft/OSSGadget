// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Health;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    public class HealthTool : OSSGadget
    {
        #region Private Fields

        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-health";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(HealthTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>
        /// Command line options
        /// </summary>
        private readonly Dictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "format", "text" },
            { "output-file", null }
        };

        #endregion Private Fields

        #region Public Constructors

        public HealthTool() : base()
        {
        }

        #endregion Public Constructors

        #region Public Methods

        public async Task<HealthMetrics?> CheckHealth(PackageURL purl)
        {
            var packageManager = ProjectManagerFactory.CreateProjectManager(purl, null);

            if (packageManager != null)
            {
                var content = await packageManager.GetMetadata(purl);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    RepoSearch repoSearcher = new RepoSearch();
                    foreach (var (githubPurl, _) in await repoSearcher.ResolvePackageLibraryAsync(purl))
                    {
                        try
                        {
                            var healthAlgorithm = new GitHubHealthAlgorithm(githubPurl);
                            var health = await healthAlgorithm.GetHealth();
                            return health;
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "Unable to calculate health for {0}: {1}", githubPurl, ex.Message);
                        }
                    }
                }
                else
                {
                    Logger.Warn("No metadata found for {0}", purl.ToString());
                }
            }
            else
            {
                throw new ArgumentException("Invalid Package URL type: {0}", purl.Type);
            }
            return null;
        }

        #endregion Public Methods

        #region Private Methods

        private static void Main(string[] args)
        {
            var healthTool = new HealthTool();
            Logger.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            healthTool.ParseOptions(args);

            // output to console or file?
            bool redirectConsole = !string.IsNullOrEmpty((string?)healthTool.Options["output-file"]);
            if (redirectConsole && healthTool.Options["output-file"] is string outputLoc)
            {
                if (!ConsoleHelper.RedirectConsole(outputLoc))
                {
                    Logger.Error("Could not switch output from console to file");
                    // continue with current output
                }
            }

            // select output format
            string format = ((string?)healthTool.Options["format"] ?? string.Empty).ToLower();
            OutputBuilder outputBuilder;
            try
            {
                outputBuilder = new OutputBuilder(format);
            }
            catch (ArgumentOutOfRangeException)
            {
                Logger.Error("Invalid output format");
                return;
            }

            if (healthTool.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        var healthMetrics = healthTool.CheckHealth(purl).Result;
                        healthTool.AppendOutput(outputBuilder, purl, healthMetrics);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            }
            else
            {
                Logger.Warn("No target provided; nothing to check health of.");
                HealthTool.ShowUsage();
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
    package-url                 PackgeURL specifier to check health for (required, repeats OK)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --format                      selct the output format (text|sarifv1|sarifv2). (default is text)
  --output-file                 send the command output to a file instead of stdout
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }

        private void AppendOutput(OutputBuilder outputBuilder, PackageURL purl, HealthMetrics? healthMetrics)
        {
            if (outputBuilder.isTextFormat())
            {
                outputBuilder.AppendOutput($"Health for {purl} (via {purl})\n");
                outputBuilder.AppendOutput(healthMetrics?.ToString() ?? string.Empty);
            }
            else
            {
                outputBuilder.AppendOutput(healthMetrics?.toSarif() ?? Array.Empty<Result>().ToList());
            }
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

                    case "-v":
                    case "--version":
                        Console.Error.WriteLine($"{TOOL_NAME} {VERSION}");
                        Environment.Exit(1);
                        break;

                    default:
                        if (Options["target"] is IList<string> innerTargetList)
                        {
                            innerTargetList.Add(args[i]);
                        }
                        break;
                }
            }
        }

        #endregion Private Methods
    }
}