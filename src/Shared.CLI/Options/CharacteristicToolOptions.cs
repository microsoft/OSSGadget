// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CodeAnalysis.Sarif;
using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("characteristic", HelpText = "Run risk calculator tool")]
public class CharacteristicToolOptions : BaseToolOptions
{
        [Usage()]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Find the characterstics for the given package",
                    new CharacteristicToolOptions() { Targets = new List<string>() {"[options]", "package-url..." } })};
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

        [Option('b', "backtracking", Required = false, HelpText = "Use backtracking regex engine by default.")]
        public bool EnableBacktracking { get; set; } = false;

        [Option('s', "single-threaded", Required = false, HelpText = "Use single-threaded analysis")]
        public bool SingleThread { get; set; } = false;

        public bool AllowTagsInBuildFiles { get; set; } = true;

        public bool AllowDupTags { get; set; } = false;

        public FailureLevel SarifLevel { get; set; } = FailureLevel.Note;
}