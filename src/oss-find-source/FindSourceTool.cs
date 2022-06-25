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
    using PackageManagers;
    using PackageUrl;

    public class FindSourceTool : OSSGadget
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

        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find the source code repository for the given package", new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

            [Option('f', "format", Required = false, Default = "text",
                            HelpText = "specify the output format(text|sarifv1|sarifv2)")]
            public string Format { get; set; } = "text";

            [Option('o', "output-file", Required = false, Default = "",
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; } = "";

            [Option('S', "single", Required = false, Default = false,
                HelpText = "Show only top possibility of the package source repositories. When using text format the *only* output will be the URL or empty string if error or not found.")]
            public bool Single { get; set; }

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }
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
            await findSourceTool.ParseOptions<Options>(args).WithParsedAsync(findSourceTool.RunAsync);
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

        private async Task RunAsync(Options options)
        {
            // Save the console logger to restore it later if we are in single mode
            NLog.Targets.Target? oldConfig = LogManager.Configuration.FindTargetByName("consoleLog");
            if (!options.Single)
            {
                ShowToolBanner();
            }
            else
            {
                // Suppress console logging for single mode
                LogManager.Configuration.RemoveTarget("consoleLog");
            }
            // select output destination and format
            SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(options.Format);
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL? purl = new PackageURL(target);
                        Dictionary<PackageURL, double> dictionary = await FindSourceAsync(purl);
                        List<KeyValuePair<PackageURL, double>>? results = dictionary.ToList();
                        results.Sort((b, a) => a.Value.CompareTo(b.Value));
                        if (options.Single)
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
            if (options.Single)
            {
                LogManager.Configuration.AddTarget(oldConfig);
            }
        }
    }
}