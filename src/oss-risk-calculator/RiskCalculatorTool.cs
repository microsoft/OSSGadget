// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Health;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    internal class RiskCalculatorTool : OSSGadget
    {
        #region Public Fields

        /// <summary>
        /// Command line options
        /// </summary>
        public Dictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "external-risk", 0 },
            { "download-directory", null },
            { "use-cache", false }
        };

        #endregion Public Fields

        #region Private Fields

        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-risk-calculator";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(RiskCalculatorTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        #endregion Private Fields

        #region Public Constructors

        public RiskCalculatorTool() : base()
        {
        }

        #endregion Public Constructors

        #region Public Methods

        public async Task<double> CalculateRisk(PackageURL purl, string? targetDirectory, bool doCaching)
        {
            Logger?.Trace("CalculateRisk({0})", purl.ToString());

            var characteristicTool = new CharacteristicTool();
            var characteristics = characteristicTool.AnalyzePackage(purl, targetDirectory, doCaching).Result;

            var healthTool = new HealthTool();
            var healthMetrics = healthTool.CheckHealth(purl).Result;
            if (healthMetrics == null)
            {
                Logger?.Warn("Unable to determine health metrics, will use a default of 0");
                healthMetrics = new HealthMetrics(purl)
                {
                    SecurityIssueHealth = 0,
                    CommitHealth = 0,
                    ContributorHealth = 0,
                    IssueHealth = 0,
                    ProjectSizeHealth = 0,
                    PullRequestHealth = 0,
                    RecentActivityHealth = 0,
                    ReleaseHealth = 0
                };
            }
            // Risk calculation algorithm
            var aggregateRisk = (
                5 * healthMetrics.SecurityIssueHealth +
                healthMetrics.ContributorHealth
            ) / 600.0;

            var highRiskTags = new string[] { "Cryptography." };
            bool isHighRisk = false;
            foreach (var charKey in characteristics.Keys)
            {
                if (characteristics[charKey]?.Metadata.UniqueTags is ConcurrentDictionary<string, byte> dict)
                {
                    foreach (var tag in dict)
                    {
                        isHighRisk |= highRiskTags.Any(t => tag.Key.StartsWith(t));
                    }
                }
            }
            if (isHighRisk)
            {
                aggregateRisk += 30.0;
            }
            return aggregateRisk;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        private static void Main(string[] args)
        {
            var riskCalculator = new RiskCalculatorTool();

            Logger?.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            riskCalculator.ParseOptions(args);

            if (riskCalculator.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var useCache = (bool?)riskCalculator.Options["use-cache"] == true;
                        var purl = new PackageURL(target);
                        var riskLevel = riskCalculator.CalculateRisk(purl,
                            (string?)riskCalculator.Options["download-directory"],
                            useCache).Result;
                        Logger?.Info($"Risk Level: {riskLevel}");
                    }
                    catch (Exception ex)
                    {
                        Logger?.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger?.Warn("No target provided; nothing to analyze.");
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
    package-url                 PackgeURL specifier to download (required, repeats OK)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --external-risk NUMBER        include additional risk in final calculation
  --download-directory          the directory to download the package to
  --use-cache                   do not download the package if it is already present in the destination directory
  --help                        show this help message and exit
  --version                     show version of this tool
");
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

                    case "--external-risk":
                        if (double.TryParse(args[++i], out var externalRisk))
                        {
                            Options["external-risk"] = externalRisk;
                        }
                        else
                        {
                            Console.WriteLine("Invalid external-risk value: number expected.");
                            ShowUsage();
                            Environment.Exit(1);
                        }
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

        #endregion Private Methods
    }
}