// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using NLog;
using CommandLine;
using CommandLine.Text;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;
using Microsoft.ApplicationInspector.RulesEngine;

namespace Microsoft.CST.OpenSource
{
    using Microsoft.Extensions.Options;
    using OssGadget.Options;
    using OssGadget.Tools;
    using OssGadget.Tools.HealthTool;
    using PackageManagers;
    using PackageUrl;

    public class RiskCalculatorTool : BaseTool<RiskCalculatorToolOptions>
    {
        public RiskCalculatorTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public RiskCalculatorTool() : this(new ProjectManagerFactory())
        {
        }

        /// <summary>
        /// Calculates project risk based on health and characteristics.
        /// </summary>
        /// <param name="purl">Package URL to load</param>
        /// <param name="targetDirectory">Target directory to download content to (default: temporary location)</param>
        /// <param name="doCaching">Cache the project for later processing (default: false)</param>
        /// <returns></returns>
        public async Task<double> CalculateRisk(PackageURL purl, string? targetDirectory, bool doCaching = false, bool checkHealth = true)
        {
            Logger.Trace("CalculateRisk({0})", purl.ToString());

            CharacteristicTool? characteristicTool = new CharacteristicTool(ProjectManagerFactory);
            CharacteristicToolOptions? cOptions = new CharacteristicToolOptions();
            Dictionary<string, AnalyzeResult?>? characteristics = characteristicTool.AnalyzePackage(cOptions, purl, targetDirectory, doCaching).Result;
            double aggregateRisk = 0.0;

            if (checkHealth)
            {
                HealthTool? healthTool = new HealthTool(ProjectManagerFactory);
                HealthMetrics? healthMetrics = await healthTool.CheckHealth(purl);
                if (healthMetrics == null)
                {
                    Logger.Warn("Unable to determine health metrics, will use a default of 0.");
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
                Logger.Trace("Health Metrics:\n{}", healthMetrics);

                // Risk calculation algorithm: Weight each of the health scores.
                aggregateRisk = 1.0 - (
                    5.0 * healthMetrics.SecurityIssueHealth / 100.0 +
                    1.0 * healthMetrics.CommitHealth / 100.0 +
                    3.0 * healthMetrics.IssueHealth / 100.0 +
                    2.0 * healthMetrics.PullRequestHealth / 100.0 +
                    0.25 * healthMetrics.RecentActivityHealth / 100.0 +
                    1.0 * healthMetrics.ContributorHealth / 100.0
                ) / 12.25;
                Logger.Trace("Aggregate Health Risk: {}", aggregateRisk);
            }

            string[]? highRiskTags = new string[] { "Cryptography.", "Authentication.", "Authorization.", "Data.Deserialization." };
            Dictionary<string, int>? highRiskTagsSeen = new Dictionary<string, int>();

            foreach (AnalyzeResult? analyzeResult in characteristics.Values)
            {
                foreach (MatchRecord? match in analyzeResult?.Metadata?.Matches ?? new List<MatchRecord>())
                {
                    foreach (string? tag in match.Tags ?? Array.Empty<string>())
                    {
                        foreach (string? highRiskTag in highRiskTags)
                        {
                            if (tag.StartsWith(highRiskTag))
                            {
                                if (!highRiskTagsSeen.ContainsKey(highRiskTag))
                                {
                                    highRiskTagsSeen[highRiskTag] = 0;
                                }
                                highRiskTagsSeen[highRiskTag]++;
                            }
                        }
                    }
                }
            }
            if (Logger.IsTraceEnabled)
            {
                Logger.Trace("Found {} high-risk tags over {} categories.", highRiskTagsSeen.Values.Sum(), highRiskTagsSeen.Keys.Count());
            }

            double highRiskTagRisk = (
                0.4 * highRiskTagsSeen.Keys.Count() +
                0.6 * Math.Min(highRiskTagsSeen.Values.Sum(), 5)
            );
            highRiskTagRisk = highRiskTagRisk > 1.0 ? 1.0 : highRiskTagRisk;

            aggregateRisk = (
                0.7 * aggregateRisk +
                0.7 * highRiskTagRisk
            );
            aggregateRisk = aggregateRisk > 1.0 ? 1.0 : aggregateRisk;

            Logger.Trace("Final Risk: {}", aggregateRisk);

            return aggregateRisk;
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
                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(GetSarifResults(purl, riskLevel));
                    break;

                case OutputFormat.text:
                default:
                    string? riskDescription = "low";
                    if (riskLevel > 0.50) riskDescription = "medium";
                    if (riskLevel > 0.80) riskDescription = "high";
                    if (riskLevel > 0.90) riskDescription = "very high";

                    outputBuilder.AppendOutput(new string[] { $"Risk Level: {riskLevel:N2} ({riskDescription})" });
                    break;
            }
        }

        public override async Task<ErrorCode> RunAsync(RiskCalculatorToolOptions options)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(options.Format);
            
            // Support for --verbose
            if (options.Verbose)
            {
                NLog.Config.LoggingRule? consoleLog = LogManager.Configuration.FindRuleByName("consoleLog");
                consoleLog.SetLoggingLevels(LogLevel.Trace, LogLevel.Fatal);
            }

            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        bool useCache = options.UseCache == true;
                        PackageURL? purl = new PackageURL(target);
                        string downloadDirectory = options.DownloadDirectory == "." ? System.IO.Directory.GetCurrentDirectory() : options.DownloadDirectory;
                        double riskLevel = CalculateRisk(purl,
                            downloadDirectory,
                            useCache,
                            !options.NoHealth).Result;
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

            return ErrorCode.Ok;
        }
    }
}