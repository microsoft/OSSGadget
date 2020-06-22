// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Health;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource
{
    internal class RiskCalculatorTool : OSSGadget
    {
        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Calculate a risk metric for the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

            [Option('r', "external-risk", Required = false, Default = 0,
                HelpText = "include additional risk in final calculation.")]
            public int ExternalRisk { get; set; }

            [Option('d', "download-directory", Required = false, Default = null,
                HelpText = "the directory to download the package to.")]
            public string? DownloadDirectory { get; set; }

            [Option('f', "format", Required = false, Default = "text",
                                                                HelpText = "selct the output format(text|sarifv1|sarifv2)")]
            public string Format { get; set; } = "text";

            [Option('o', "output-file", Required = false, Default = "",
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; } = "";

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }

            [Option('c', "use-cache", Required = false, Default = false,
                            HelpText = "do not download the package if it is already present in the destination directory.")]
            public bool UseCache { get; set; }
        }

        public RiskCalculatorTool() : base()
        {
        }

        public async Task<double> CalculateRisk(PackageURL purl, string? targetDirectory, bool doCaching)
        {
            Logger.Trace("CalculateRisk({0})", purl.ToString());

            var characteristicTool = new CharacteristicTool();
            var cOptions = new CharacteristicTool.Options();
            var characteristics = characteristicTool.AnalyzePackage(cOptions, purl, targetDirectory, doCaching).Result;

            var healthTool = new HealthTool();
            var healthMetrics = healthTool.CheckHealth(purl).Result;
            if (healthMetrics == null)
            {
                Logger.Warn("Unable to determine health metrics, will use a default of 0");
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

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        private static async Task Main(string[] args)
        {
            var riskCalculator = new RiskCalculatorTool();
            await riskCalculator.ParseOptions<Options>(args).WithParsedAsync(riskCalculator.RunAsync);
        }
        private async Task RunAsync(Options options)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(options.Format);

            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var useCache = options.UseCache == true;
                        var purl = new PackageURL(target);
                        var riskLevel = CalculateRisk(purl,
                            options.DownloadDirectory,
                            useCache).Result;
                        AppendOutput(outputBuilder, purl, riskLevel);

                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                    outputBuilder.PrintOutput();
                }

                RestoreOutput();
            }
        }
        /// <summary>
        ///     Convert charactersticTool results into output format
        /// </summary>
        /// <param name="outputBuilder"> </param>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        private void AppendOutput(IOutputBuilder outputBuilder, PackageURL purl, double riskLevel)
        {
            switch (currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    outputBuilder.AppendOutput(new string[] { $"Risk Level: {riskLevel}" });

                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(GetSarifResults(purl, riskLevel));
                    break;
            }
        }

        /// <summary>
        ///     Build and return a list of Sarif Result list from the find characterstics results
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<SarifResult> GetSarifResults(PackageURL purl, double riskLevel)
        {
            List<SarifResult> sarifResults = new List<SarifResult>();
            SarifResult sarifResult = new SarifResult()
            {
                Kind = ResultKind.Informational,
                Level = FailureLevel.None,
                Locations = SarifOutputBuilder.BuildPurlLocation(purl),
                Rank = riskLevel
            };

            sarifResults.Add(sarifResult);
            return sarifResults;
        }
    }
}