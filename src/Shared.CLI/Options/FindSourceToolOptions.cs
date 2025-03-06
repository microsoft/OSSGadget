// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("find-source", HelpText = "Find the source code repository for the given package")]
public class FindSourceToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Find the source code repository for the given package", new FindSourceToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }

    [Option('f', "format", Required = false, Default = "text",
        HelpText = "specify the output format(text|sarifv1|sarifv2)")]
    public string Format { get; set; } = "text";

    [Option('o', "output-file", Required = false, Default = "",
        HelpText = "send the command output to a file instead of stdout")]
    public string OutputFile { get; set; } = "";

    [Option('S', "single", Required = false, Default = false,
        HelpText = "Show only top possibility of the package source repositories. When using text format the *only* output will be the URL or empty string if error or not found.")]
    public bool Single { get; set; }

    [Value(0, Required = true,
        HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }
}
