// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.ApplicationInspector.RulesEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Crayon.Output;

namespace Microsoft.CST.OpenSource
{
    using PackageManagers;

    public class DetectBackdoorTool : OSSGadget
    {
        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Identify potential malware or backdoors in the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

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
        }

        public DetectBackdoorTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
            RULE_DIRECTORY = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "BackdoorRules");
        }

        public DetectBackdoorTool() : this(new ProjectManagerFactory())
        {
        }

        /// <summary>
        ///     Location of the backdoor detection rules.
        /// </summary>
        private string RULE_DIRECTORY { get; set; }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        private static async Task Main(string[] args)
        {
            ShowToolBanner();

            DetectBackdoorTool? detectBackdoorTool = new DetectBackdoorTool();
            Options? parsedOptions = detectBackdoorTool.ParseOptions<Options>(args).Value;
            List<Dictionary<string, AnalyzeResult?>>? detectionResults = await detectBackdoorTool.RunAsync(parsedOptions);

            foreach (Dictionary<string, AnalyzeResult?>? result in detectionResults)
            {
                foreach (KeyValuePair<string, AnalyzeResult?> entry in result)
                {
                    if (entry.Value == null || entry.Value.Metadata == null || entry.Value.Metadata.Matches == null)
                    {
                        continue;
                    }

                    if (parsedOptions.Format == "text")
                    {
                        IOrderedEnumerable<MatchRecord>? matchEntries = entry.Value.Metadata.Matches.OrderByDescending(x => x.Confidence);
                        int matchEntriesCount = matchEntries.Count();
                        int matchIndex = 1;

                        foreach (MatchRecord? match in matchEntries)
                        {
                            WriteMatch(match, matchIndex, matchEntriesCount);
                            matchIndex++;
                        }
                        Console.WriteLine($"{entry.Value.Metadata.TotalMatchesCount} matches found.");
                    }

                    void WriteMatch(MatchRecord match, int index, int matchCount)
                    {
                        string? filename = match.FileName;
                        if (filename == null)
                        {
                            return;
                        }
                        int? sourcePathLength = entry.Value.Metadata.SourcePath?.Length;
                        if (sourcePathLength.HasValue)
                        {
                            if (entry.Value.Metadata.SourcePath != null && filename.StartsWith(entry.Value.Metadata.SourcePath))
                            {
                                filename = filename[sourcePathLength.Value..];
                            }
                        }
                        Console.WriteLine(Red($"--[ ") + Blue("Match #") + Yellow(index.ToString()) + Blue(" of ") + Yellow(matchCount.ToString()) + Red(" ]--"));
                        Console.WriteLine("   Rule Id: " + Blue(match.Rule.Id));
                        Console.WriteLine("       Tag: " + Blue(match.Tags?.First()));
                        Console.WriteLine("  Severity: " + Cyan(match.Severity.ToString()) + ", Confidence: " + Cyan(match.Confidence.ToString()));
                        Console.WriteLine("  Filename: " + Yellow(filename));
                        Console.WriteLine("   Pattern: " + Green(match.MatchingPattern.Pattern));
                        foreach (string? line in match.Excerpt.Split(Environment.NewLine, StringSplitOptions.None))
                        {
                            string? s = line;
                            if (s.Length > 100)
                            {
                                s = s.Substring(0, 100);
                            }
                            Console.WriteLine(Bright.Black("  | ") + Magenta(s));
                        }
                        Console.WriteLine();
                    }
                }
            }
        }

        private async Task<List<Dictionary<string, AnalyzeResult?>>> RunAsync(Options options)
        {
            if (options != null && options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                CharacteristicTool? characteristicTool = new CharacteristicTool(ProjectManagerFactory);
                CharacteristicTool.Options cOptions = new CharacteristicTool.Options
                {
                    Targets = options.Targets,
                    DisableDefaultRules = true,
                    CustomRuleDirectory = RULE_DIRECTORY,
                    DownloadDirectory = options.DownloadDirectory,
                    UseCache = options.UseCache,
                    Format = options.Format == "text" ? "none" : options.Format,
                    OutputFile = options.OutputFile,
                    AllowTagsInBuildFiles = true,
                    FilePathExclusions = ".md,LICENSE,.txt",
                    AllowDupTags = true,
                    SarifLevel = CodeAnalysis.Sarif.FailureLevel.Warning
                };

                return await characteristicTool.RunAsync(cOptions);
            }
            else
            {
                return new List<Dictionary<string, AnalyzeResult?>>();
            }
        }
    }
}
