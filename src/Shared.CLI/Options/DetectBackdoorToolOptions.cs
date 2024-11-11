// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("detect-backdoor", HelpText = "Run detect backdoor tool")]
public class DetectBackdoorToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Identify potential malware or backdoors in the given package",
                    new DetectBackdoorToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
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

    [Option('b', "backtracking", Required = false, HelpText = "Use backtracking engine by default.")]
    public bool EnableBacktracking { get; set; } = false;

    [Option('s', "single-threaded", Required = false, HelpText = "Use single-threaded analysis")]
    public bool SingleThread { get; set; } = false;
}