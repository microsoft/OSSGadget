// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

namespace Microsoft.CST.OpenSource
{
    public class FindSourceTool : OSSGadget
    {
        public class Options
        {
            [Option('f', "format", Required = false, Default = "text",
                HelpText = "selct the output format(text|sarifv1|sarifv2)")]
            public string Format { get; set; } = "text";

            [Option('o', "output-file", Required = false, Default = "",
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; } = "";

            [Option('s', "show-all", Required = false, Default = false,
                HelpText = "show all possibilities of the package source repositories")]
            public bool ShowAll { get; set; }

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }

            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find the source code repository for the given package", new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }
        }

        private static async Task Main(string[] args)
        {
            var findSourceTool = new FindSourceTool();
            await findSourceTool.ParseOptions<Options>(args).WithParsedAsync(findSourceTool.RunAsync);
        }

        private async Task RunAsync(Options options)
        {
            // select output destination and format
            this.SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = this.SelectFormat(options.Format);
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        var results = FindSource(purl).Result.ToList();
                        results.Sort((b, a) => a.Value.CompareTo(b.Value));
                        AppendOutput(outputBuilder, purl, results);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            }
            this.RestoreOutput();
        }

        public FindSourceTool() : base()
        {
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
                RepoSearch repoSearcher = new RepoSearch();
                var repos = await (repoSearcher.ResolvePackageLibraryAsync(purl) ??
                    Task.FromResult(new Dictionary<PackageURL, double>()));
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
        ///     Convert findSourceTool results into text format
        /// </summary>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<string> GetTextResults(List<KeyValuePair<PackageURL, double>> results)
        {
            //StringBuilder stringOutput = new StringBuilder();
            List<string> stringOutput = new List<string>();
            foreach (var result in results)
            {
                var confidence = result.Value * 100.0;
                stringOutput.Add(
                    $"{confidence:0.0}%\thttps://github.com/{result.Key.Namespace}/{result.Key.Name} ({result.Key})");
            }
            return stringOutput;
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
            foreach (var result in results)
            {
                var confidence = result.Value * 100.0;
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
    }
}