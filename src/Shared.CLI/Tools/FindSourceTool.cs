// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

namespace Microsoft.CST.OpenSource
{
    using OssGadget.Options;
    using PackageManagers;
    using PackageUrl;

    public class FindSourceTool : BaseTool<FindSourceToolOptions>
    {
        private static readonly HashSet<string> IGNORE_PURLS = new()
        {
            "pkg:github/metacpan/metacpan-web"
        };

        public FindSourceTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public FindSourceTool() : this(new ProjectManagerFactory())
        {
        }

        public async Task<Dictionary<PackageURL, double>> FindSourceAsync(PackageURL purl)
        {
            Logger.Trace("FindSourceAsync({0})", purl);

            Dictionary<PackageURL, double>? repositoryMap = new Dictionary<PackageURL, double>();

            if (purl == null)
            {
                Logger.Warn("FindSourceAsync was passed an invalid purl.");
                return repositoryMap;
            }

            PackageURL? purlNoVersion = new PackageURL(purl.Type, purl.Namespace, purl.Name,
                                               null, purl.Qualifiers, purl.Subpath);
            Logger.Debug("Searching for source code for {0}", purlNoVersion.ToString());

            try
            {
                RepoSearch repoSearcher = new RepoSearch(ProjectManagerFactory);
                Dictionary<PackageURL, double>? repos = await (repoSearcher.ResolvePackageLibraryAsync(purl) ??
                    Task.FromResult(new Dictionary<PackageURL, double>()));

                foreach (string? ignorePurl in IGNORE_PURLS)
                {
                    repos.Remove(new PackageURL(ignorePurl));
                }

                if (repos.Any())
                {
                    foreach (PackageURL? key in repos.Keys)
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
        ///     Build and return a list of Sarif Result list from the find source results
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<Result> GetSarifResults(PackageURL purl, List<KeyValuePair<PackageURL, double>> results)
        {
            List<Result> sarifResults = new List<Result>();
            foreach (KeyValuePair<PackageURL, double> result in results)
            {
                double confidence = result.Value * 100.0;
                Result sarifResult = new Result()
                {
                    Message = new Message()
                    {
                        Text = $"https://github.com/{result.Key.Namespace}/{result.Key.Name}"
                    },
                    Kind = ResultKind.Informational,
                    Level = FailureLevel.None,
                    Rank = confidence,
                    Locations = SarifOutputBuilder.BuildPurlLocation(purl)
                };

                sarifResults.Add(sarifResult);
            }
            return sarifResults;
        }

        /// <summary>
        ///     Convert findSourceTool results into text format
        /// </summary>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<string> GetTextResults(List<KeyValuePair<PackageURL, double>> results)
        {
            List<string> stringOutput = new List<string>();
            foreach (KeyValuePair<PackageURL, double> result in results)
            {
                double confidence = result.Value * 100.0;
                stringOutput.Add(
                    $"{confidence:0.0}%\thttps://github.com/{result.Key.Namespace}/{result.Key.Name} ({result.Key})");
            }
            return stringOutput;
        }

        static async Task Main(string[] args)
        {
            FindSourceTool findSourceTool = new FindSourceTool();
            await findSourceTool.ParseOptions<FindSourceToolOptions>(args).WithParsedAsync(findSourceTool.RunAsync);
        }

        /// <summary>
        ///     Convert findSourceTool results into output format
        /// </summary>
        /// <param name="outputBuilder"> </param>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        private void AppendOutput(IOutputBuilder outputBuilder, PackageURL purl, List<KeyValuePair<PackageURL, double>> results)
        {
            switch (currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    outputBuilder.AppendOutput(GetTextResults(results));
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(GetSarifResults(purl, results));
                    break;
            }
        }

        /// <summary>
        ///     Convert findSourceTool results into output format
        /// </summary>
        /// <param name="outputBuilder"> </param>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        private void AppendSingleOutput(IOutputBuilder outputBuilder, PackageURL purl, KeyValuePair<PackageURL, double> result)
        {
            switch (currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    outputBuilder.AppendOutput(new string[] { $"https://github.com/{result.Key.Namespace}/{result.Key.Name}" });
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(GetSarifResults(purl, new List<KeyValuePair<PackageURL, double>>() { result }));
                    break;
            }
        }

        public override async Task<ErrorCode> RunAsync(FindSourceToolOptions findSourceToolOptions)
        {
            // Save the console logger to restore it later if we are in single mode
            NLog.Targets.Target? oldConfig = LogManager.Configuration.FindTargetByName("consoleLog");
            if (findSourceToolOptions.Single)
            {
                // Suppress console logging for single mode
                LogManager.Configuration.RemoveTarget("consoleLog");
            }
            // select output destination and format
            SelectOutput(findSourceToolOptions.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(findSourceToolOptions.Format);
            if (findSourceToolOptions.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL? purl = new PackageURL(target);
                        Dictionary<PackageURL, double> dictionary = await FindSourceAsync(purl);
                        List<KeyValuePair<PackageURL, double>>? results = dictionary.ToList();
                        results.Sort((b, a) => a.Value.CompareTo(b.Value));
                        if (findSourceToolOptions.Single)
                        {
                            AppendSingleOutput(outputBuilder, purl, results[0]);
                        }
                        else
                        {
                            AppendOutput(outputBuilder, purl, results);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            }
            RestoreOutput();
            // Restore console logging if we were in single mode
            if (findSourceToolOptions.Single)
            {
                LogManager.Configuration.AddTarget(oldConfig);
            }

            return ErrorCode.Ok;
        }
    }
}