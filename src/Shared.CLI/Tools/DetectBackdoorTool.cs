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
    using OssGadget.Options;
    using OssGadget.Tools;
    using PackageManagers;

    public class DetectBackdoorTool : BaseTool<DetectBackdoorToolOptions>
    {
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
        public override async Task<ErrorCode> RunAsync(DetectBackdoorToolOptions options)
        {
            List<Dictionary<string, AnalyzeResult?>>? detectionResults = await LegacyRunAsync(options);

            foreach (Dictionary<string, AnalyzeResult?>? result in detectionResults)
            {
                foreach (KeyValuePair<string, AnalyzeResult?> entry in result)
                {
                    if (entry.Value == null || entry.Value.Metadata == null || entry.Value.Metadata.Matches == null)
                    {
                        continue;
                    }

                    if (options.Format == "text")
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

                        string[] FullTextLines = match.FullTextContainer.FullContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        string[] ExcerptsLines = match.Excerpt.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                        int ExcerptStart = -1;

                        for (int i = 0; i < ExcerptsLines.Length; i++)
                        {
                            if (string.Equals(ExcerptsLines[i].TrimStart().TrimEnd(), FullTextLines[match.StartLocationLine - 1].TrimStart().TrimEnd()))
                            {
                                ExcerptStart = match.StartLocationLine - i;
                                break;
                            }
                        }

                        Array.Resize(ref ExcerptsLines, ExcerptsLines.Length - 1);
                        foreach (string? line in ExcerptsLines)
                        {
                            string? s = line;
                            if (s.Length > 100)
                            {
                                s = s.Substring(0, 100);
                            }

                            if (ExcerptStart != -1)
                            {
                                Console.WriteLine(Bright.Black($"{ExcerptStart++} | ") + Magenta(s));
                            }
                            else
                            {
                                Console.WriteLine(Bright.Black("  | ") + Magenta(s));
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }

            return ErrorCode.Ok;
        }
        
        private async Task<List<Dictionary<string, AnalyzeResult?>>> LegacyRunAsync(DetectBackdoorToolOptions options)
        {
            if (options != null && options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                CharacteristicTool? characteristicTool = new CharacteristicTool(ProjectManagerFactory);
                CharacteristicToolOptions cOptions = new CharacteristicToolOptions
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
                    SarifLevel = CodeAnalysis.Sarif.FailureLevel.Warning,
                    EnableBacktracking = options.EnableBacktracking,
                    SingleThread = options.SingleThread
                };

                return await characteristicTool.LegacyRunAsync(cOptions);
            }
            else
            {
                return new List<Dictionary<string, AnalyzeResult?>>();
            }
        }
    }
}
