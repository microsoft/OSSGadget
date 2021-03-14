// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
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
                        new Example("Find the characterstics for the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

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

        public DetectBackdoorTool() : base()
        {
            RULE_DIRECTORY = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "BackdoorRules");
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
            var detectBackdoorTool = new DetectBackdoorTool();
            var parsedOptions = detectBackdoorTool.ParseOptions<Options>(args).Value;
            var detectionResults = await detectBackdoorTool.RunAsync(parsedOptions);

            foreach (var result in detectionResults)
            {
                foreach (var entry in result)
                {
                    if (entry.Value == null || entry.Value.Metadata == null || entry.Value.Metadata.Matches == null)
                    {
                        continue;
                    }

                    foreach (var match in entry.Value.Metadata.Matches)
                    {
                        var filename = match.FileName;
                        if (filename == null)
                        {
                            continue;
                        }
                        var sourcePathLength = entry.Value.Metadata.SourcePath?.Length;
                        if (sourcePathLength.HasValue)
                        {
                            if (entry.Value.Metadata.SourcePath != null && filename.StartsWith(entry.Value.Metadata.SourcePath))
                            {
                                filename = filename[sourcePathLength.Value..];
                            }
                        }
                        if (parsedOptions.Format == "text")
                        {
                            Console.WriteLine($"{match.Tags?.First()} - {filename}:{match.StartLocationLine} - {match.RuleName}");
                        }
                    }
                }
            }
        }

        private async Task<List<Dictionary<string, AnalyzeResult?>>> RunAsync(Options options)
        {
            if (options != null && options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                var characteristicTool = new CharacteristicTool();
                CharacteristicTool.Options cOptions = new CharacteristicTool.Options();
                cOptions.Targets = options.Targets;
                cOptions.DisableDefaultRules = true;
                cOptions.CustomRuleDirectory = RULE_DIRECTORY;
                cOptions.DownloadDirectory = options.DownloadDirectory;
                cOptions.UseCache = options.UseCache;
                cOptions.Format = options.Format == "text" ? "none" : options.Format;
                cOptions.OutputFile = options.OutputFile;
                cOptions.TreatEverythingAsCode = true;
                cOptions.FilePathExclusions = ".md,LICENSE,.txt";
                cOptions.AllowDupTags = true;

                return await characteristicTool.RunAsync(cOptions);
            }
            else
            {
                return new List<Dictionary<string, AnalyzeResult?>>();
            }
        }
    }
}