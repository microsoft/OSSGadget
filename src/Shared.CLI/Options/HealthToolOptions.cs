// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("health", HelpText = "Run oss-health tool")]
public class HealthToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Find the source code repository for the given package",
                    new HealthToolOptions() { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }

    [Option('f', "format", Required = false, Default = "text",
        HelpText = "specify the output format(text|sarifv1|sarifv2)")]
    public string? Format { get; set; }

    [Option('o', "output-file", Required = false, Default = "",
        HelpText = "send the command output to a file instead of stdout")]
    public string OutputFile { get; set; } = "";

    [Value(0, Required = true,
        HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }
}