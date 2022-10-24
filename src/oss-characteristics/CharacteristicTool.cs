// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.ApplicationInspector.RulesEngine;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource
{
    using PackageManagers;
    using PackageUrl;

    public class CharacteristicTool : OSSGadget
    {
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

            [Option("disable-default-rules", Required = false, Default = false,
                HelpText = "do not load default, built-in rules.")]
            public bool DisableDefaultRules { get; set; }

            [Option('d', "download-directory", Required = false, Default = ".",
                HelpText = "the directory to download the package to.")]
            public string DownloadDirectory { get; set; } = ".";

            [Option('f', "format", Required = false, Default = "text",
                HelpText = "specify the output format(text|sarifv1|sarifv2)")]
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

            [Option('x', "exclude", Required = false,
                HelpText = "exclude files or paths which match provided glob patterns.")]
            public string FilePathExclusions { get; set; } = "";

            public bool AllowTagsInBuildFiles { get; set; } = true;

            public bool AllowDupTags { get; set; } = false;

            public FailureLevel SarifLevel { get; set; } = FailureLevel.Note;
        }

        public CharacteristicTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public CharacteristicTool() : this(new ProjectManagerFactory()) 
        {
        }

        public async Task<AnalyzeResult?> AnalyzeFile(Options options, string file)
        {
            Logger.Trace("AnalyzeFile({0})", file);
            return await AnalyzeDirectory(options, file);
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
            AnalyzeOptions? analyzeOptions = new AnalyzeOptions()
            {
                SourcePath = new[] { directory },
                IgnoreDefaultRules = options.DisableDefaultRules,
                CustomRulesPath = options.CustomRuleDirectory,
                ConfidenceFilters = new [] { Confidence.High | Confidence.Medium | Confidence.Low },
                ScanUnknownTypes = true,
                AllowAllTagsInBuildFiles = options.AllowTagsInBuildFiles,
                SingleThread = false,
                FilePathExclusions = options.FilePathExclusions?.Split(',') ?? Array.Empty<string>()
            };

            try
            {
                AnalyzeCommand? analyzeCommand = new AnalyzeCommand(analyzeOptions);
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
            bool doCaching = false)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());

            Dictionary<string, AnalyzeResult?>? analysisResults = new Dictionary<string, AnalyzeResult?>();

            PackageDownloader? packageDownloader = new PackageDownloader(purl, ProjectManagerFactory, targetDirectoryName, doCaching);
            // ensure that the cache directory has the required package, download it otherwise
            List<string>? directoryNames = await packageDownloader.DownloadPackageLocalCopy(purl,
                false,
                true);
            if (directoryNames.Count > 0)
            {
                foreach (string? directoryName in directoryNames)
                {
                    AnalyzeResult? singleResult = await AnalyzeDirectory(options, directoryName);
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

        /// <summary>
        ///     Build and return a list of Sarif Result list from the find characterstics results
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<SarifResult> GetSarifResults(PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResult, Options opts)
        {
            List<SarifResult> sarifResults = new List<SarifResult>();
            
            if (analysisResult.HasAtLeastOneNonNullValue())
            {
                foreach (string? key in analysisResult.Keys)
                {
                    MetaData? metadata = analysisResult?[key]?.Metadata;

                    foreach (MatchRecord? result in metadata?.Matches ?? new List<MatchRecord>())
                    {
                        SarifResult? individualResult = new SarifResult()
                        {
                            Message = new Message()
                            {
                                Text = result.RuleDescription,
                                Id = result.RuleId
                            },
                            Kind = ResultKind.Informational,
                            Level = opts.SarifLevel,
                            Locations = SarifOutputBuilder.BuildPurlLocation(purl),
                            Rule = new ReportingDescriptorReference() { Id = result.RuleId },
                        };

                        individualResult.SetProperty("Severity", result.Severity);
                        individualResult.SetProperty("Confidence", result.Confidence);

                        individualResult.Locations.Add(new CodeAnalysis.Sarif.Location()
                        {
                            PhysicalLocation = new PhysicalLocation()
                            {
                                Address = new Address() { FullyQualifiedName = result.FileName },
                                Region = new Region()
                                {
                                    StartLine = result.StartLocationLine,
                                    EndLine = result.EndLocationLine,
                                    StartColumn = result.StartLocationColumn,
                                    EndColumn = result.EndLocationColumn,
                                    SourceLanguage = result.Language,
                                    Snippet = new ArtifactContent()
                                    {
                                        Text = result.Excerpt,
                                        Rendered = new MultiformatMessageString(result.Excerpt, $"`{result.Excerpt}`", null)
                                    }
                                }
                            }
                        });
                        
                        sarifResults.Add(individualResult);
                    }
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
                foreach (string? key in analysisResult.Keys)
                {
                    MetaData? metadata = analysisResult?[key]?.Metadata;

                    stringOutput.Add(string.Format("Programming Language(s): {0}",
                        string.Join(", ", metadata?.Languages?.Keys ?? new List<string>())));
                    
                    stringOutput.Add("Unique Tags (Confidence): ");
                    bool hasTags = false;
                    Dictionary<string, List<Confidence>>? dict = new Dictionary<string, List<Confidence>>();
                    foreach ((string[]? tags, Confidence confidence) in metadata?.Matches?.Where(x => x is not null).Select(x => (x.Tags, x.Confidence)) ?? Array.Empty<(string[], Confidence)>())
                    {
                        foreach (string? tag in tags)
                        {
                            if (dict.ContainsKey(tag))
                            {
                                dict[tag].Add(confidence);
                            }
                            else
                            {
                                dict[tag] = new List<Confidence>() { confidence };
                            }
                        }
                    }

                    foreach ((string? k, List<Confidence>? v) in dict)
                    {
                        hasTags = true;
                        Confidence confidence = v.Max();
                        if (confidence > 0)
                        {
                            stringOutput.Add($" * {k} ({v.Max()})");
                        }
                        else
                        {
                            stringOutput.Add($" * {k}");
                        }
                    }
                    if (!hasTags)
                    {
                        stringOutput.Add("No tags were found.");
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
            ShowToolBanner();

            CharacteristicTool? characteristicTool = new CharacteristicTool();
            await characteristicTool.ParseOptions<Options>(args).WithParsedAsync(characteristicTool.RunAsync);
        }

        /// <summary>
        ///     Convert charactersticTool results into output format
        /// </summary>
        /// <param name="outputBuilder"> </param>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        private void AppendOutput(IOutputBuilder outputBuilder, PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResults, Options opts)
        {
            switch (currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    outputBuilder.AppendOutput(GetTextResults(purl, analysisResults));
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(GetSarifResults(purl, analysisResults,opts));
                    break;
            }
        }

        public async Task<List<Dictionary<string, AnalyzeResult?>>> RunAsync(Options options)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(options.Format);

            List<Dictionary<string, AnalyzeResult?>>? finalResults = new List<Dictionary<string, AnalyzeResult?>>();

            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        if (target.StartsWith("pkg:"))
                        {
                            PackageURL? purl = new PackageURL(target);
                            string downloadDirectory = options.DownloadDirectory == "." ? System.IO.Directory.GetCurrentDirectory() : options.DownloadDirectory;
                            Dictionary<string, AnalyzeResult?>? analysisResult = await AnalyzePackage(options, purl,
                                downloadDirectory,
                                options.UseCache == true);

                            AppendOutput(outputBuilder, purl, analysisResult, options);
                            finalResults.Add(analysisResult);
                        }
                        else if (System.IO.Directory.Exists(target))
                        {
                            AnalyzeResult? analysisResult = await AnalyzeDirectory(options, target);
                            if (analysisResult != null)
                            {
                                Dictionary<string, AnalyzeResult?>? analysisResults = new Dictionary<string, AnalyzeResult?>()
                                {
                                    { target, analysisResult }
                                };
                                PackageURL? purl = new PackageURL("generic", target);
                                AppendOutput(outputBuilder, purl, analysisResults, options);
                            }
                            finalResults.Add(new Dictionary<string, AnalyzeResult?>() { { target, analysisResult } });

                        }
                        else if (File.Exists(target))
                        {
                            AnalyzeResult? analysisResult = await AnalyzeFile(options, target);
                            if (analysisResult != null)
                            {
                                Dictionary<string, AnalyzeResult?>? analysisResults = new Dictionary<string, AnalyzeResult?>()
                                {
                                    { target, analysisResult }
                                };
                                PackageURL? purl = new PackageURL("generic", target);
                                AppendOutput(outputBuilder, purl, analysisResults, options);
                            }
                            finalResults.Add(new Dictionary<string, AnalyzeResult?>() { { target, analysisResult } });
                        }
                        else
                        {
                            Logger.Warn("Package or file identifier was invalid.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            }

            RestoreOutput();
            return finalResults;
        }
    }
}
