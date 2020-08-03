// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource
{
    public class CharacteristicTool : OSSGadget
    {
        public CharacteristicTool() : base()
        {
        }

        /// <summary>
        ///     Analyzes a directory of files.
        /// </summary>
        /// <param name="directory"> directory to analyze. </param>
        /// <returns> List of tags identified </returns>
        public async Task<AnalyzeResult?> AnalyzeDirectory(Options options, string directory)
        {
            Logger.Trace("AnalyzeDirectory({0})", directory);

            AnalyzeResult? analysisResult = null;

            // Call Application Inspector using the NuGet package
            var analyzeOptions = new AnalyzeOptions()
            {
                ConsoleVerbosityLevel = "None",
                LogFileLevel = "Off",
                SourcePath = directory,
                IgnoreDefaultRules = options.DisableDefaultRules == true,
                CustomRulesPath = options.CustomRuleDirectory,
            };

            try
            {
                var analyzeCommand = new AnalyzeCommand(analyzeOptions);
                analysisResult = analyzeCommand.GetResult();
                Logger.Debug("Operation Complete: {0} files analyzed.", analysisResult?.Metadata?.TotalFiles);
            }
            catch (Exception ex)
            {
                Logger.Warn("Error analyzing {0}: {1}", directory, ex.Message);
            }

            return analysisResult;
        }

        /// <summary>
        ///     Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl"> The package-url of the package to analyze. </param>
        /// <returns> List of tags identified </returns>
        public async Task<Dictionary<string, AnalyzeResult?>> AnalyzePackage(Options options, PackageURL purl,
            string? targetDirectoryName,
            bool doCaching)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());

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
                    var singleResult = await AnalyzeDirectory(options, directoryName);
                    analysisResults[directoryName] = singleResult;
                }
            }
            else
            {
                Logger.Warn("Error downloading {0}.", purl.ToString());
            }
            packageDownloader.ClearPackageLocalCopyIfNoCaching();
            return analysisResults;
        }

        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find the characterstics for the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

            [Option('r', "custom-rule-directory", Required = false, Default = null,
                HelpText = "load rules from the specified directory.")]
            public string? CustomRuleDirectory { get; set; }

            [Option('x', "disable-default-rules", Required = false, Default = false,
                HelpText = "do not load default, built-in rules.")]
            public bool DisableDefaultRules { get; set; }

            [Option('d', "download-directory", Required = false, Default = ".",
                HelpText = "the directory to download the package to.")]
            public string DownloadDirectory { get; set; } = ".";

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

        /// <summary>
        ///     Build and return a list of Sarif Result list from the find characterstics results
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<SarifResult> GetSarifResults(PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResult)
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
                        Locations = SarifOutputBuilder.BuildPurlLocation(purl),
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
        ///     Convert charactersticTool results into text format
        /// </summary>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<string> GetTextResults(PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResult)
        {
            List<string> stringOutput = new List<string>();

            stringOutput.Add(purl.ToString());
            if (analysisResult.HasAtLeastOneNonNullValue())
            {
                StringBuilder builder = new StringBuilder();
                foreach (var key in analysisResult.Keys)
                {
                    var metadata = analysisResult?[key]?.Metadata;

                    stringOutput.Add(string.Format("Programming Language: {0}",
                        string.Join(", ", metadata?.Languages?.Keys ?? Array.Empty<string>())));
                    stringOutput.Add("Unique Tags: ");
                    foreach (var tag in metadata?.UniqueTags.Keys ?? Array.Empty<string>())
                    {
                        stringOutput.Add(string.Format($" * {tag}"));
                    }
                }
            }
            return stringOutput;
        }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        private static async Task Main(string[] args)
        {
            var characteristicTool = new CharacteristicTool();
            await characteristicTool.ParseOptions<Options>(args).WithParsedAsync(characteristicTool.RunAsync);
        }

        /// <summary>
        ///     Convert charactersticTool results into output format
        /// </summary>
        /// <param name="outputBuilder"> </param>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        private void AppendOutput(IOutputBuilder outputBuilder, PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResults)
        {
            switch (currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    outputBuilder.AppendOutput(GetTextResults(purl, analysisResults));
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(GetSarifResults(purl, analysisResults));
                    break;
            }
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
                        var purl = new PackageURL(target);
                        string downloadDirectory = options.DownloadDirectory == "." ? Directory.GetCurrentDirectory() : options.DownloadDirectory;
                        var analysisResult = AnalyzePackage(options, purl,
                            downloadDirectory,
                            options.UseCache == true).Result;

                        AppendOutput(outputBuilder, purl, analysisResult);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            }

            RestoreOutput();
        }
    }
}