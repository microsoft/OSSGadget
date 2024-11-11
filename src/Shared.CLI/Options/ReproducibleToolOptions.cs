// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("reproducible", HelpText = "Run reproducible tool")]
public class ReproducibleToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Estimate semantic equivalency of the given package and source code", new ReproducibleToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })
            };
        }
    }

    [Value(0, Required = true, HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }

    [Option('a', "all-strategies", Required = false, Default = false,
        HelpText = "Execute all strategies, even after a successful one is identified.")]
    public bool AllStrategies { get; set; }

    [Option("specific-strategies", Required = false,
        HelpText = "Execute specific strategies, comma-separated.")]
    public string? SpecificStrategies { get; set; }

    [Option('s', "source-ref", Required = false, Default = "",
        HelpText = "If a source version cannot be identified, use the specified git reference (tag, commit, etc.).")]
    public string OverrideSourceReference { get; set; } = "";

    [Option("diff-technique", Required = false, Default = DiffTechnique.Normalized, HelpText = "Configure diff technique.")]
    public DiffTechnique DiffTechnique { get; set; } = DiffTechnique.Normalized;

    [Option('o', "output-file", Required = false, Default = "", HelpText = "Send the command output to a file instead of standard output")]
    public string OutputFile { get; set; } = "";

    [Option('d', "show-differences", Required = false, Default = false,
        HelpText = "Output the differences between the package and the reference content.")]
    public bool ShowDifferences { get; set; }

    [Option("show-all-differences", Required = false, Default = false,
        HelpText = "Show all differences (default: capped at 20), implies --show-differences")]
    public bool ShowAllDifferences { get; set; }

    [Option('l', "leave-intermediate", Required = false, Default = false,
        HelpText = "Do not clean up intermediate files (useful for debugging).")]
    public bool LeaveIntermediateFiles { get; set; }
}