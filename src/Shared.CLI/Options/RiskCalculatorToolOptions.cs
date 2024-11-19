// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("risk", HelpText = "Calculate a risk metric for the given package")]
public class RiskCalculatorToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Calculate a risk metric for the given package",
                new RiskCalculatorToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }

    [Option('d', "download-directory", Required = false, Default = null,
            HelpText = "the directory to download the package to.")]
    public string DownloadDirectory { get; set; } = ".";

    [Option('r', "external-risk", Required = false, Default = 0,
                    HelpText = "include additional risk in final calculation.")]
    public int ExternalRisk { get; set; }

    [Option('f', "format", Required = false, Default = "text",
            HelpText = "specify the output format(text|sarifv1|sarifv2)")]
    public string Format { get; set; } = "text";

    [Option('o', "output-file", Required = false, Default = "",
            HelpText = "send the command output to a file instead of stdout")]
    public string OutputFile { get; set; } = "";

    [Option('n', "no-health", Required = false, Default = false,
            HelpText = "do not check project health")]
    public bool NoHealth { get; set; }

    [Option(Default = false, HelpText = "Verbose output")]
    public bool Verbose { get; set; }

    [Value(0, Required = true,
           HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }

    [Option('c', "use-cache", Required = false, Default = false,
            HelpText = "do not download the package if it is already present in the destination directory.")]
    public bool UseCache { get; set; }
}