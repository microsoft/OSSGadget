// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("fresh", HelpText = "Run fresh tool")]
public class FreshToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Find the source code repository for the given package", new FreshToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }

    [Option('f', "format", Required = false, Default = "text",
        HelpText = "specify the output format(text|sarifv1|sarifv2)")]
    public string Format { get; set; } = "text";

    [Option('o', "output-file", Required = false, Default = "",
        HelpText = "send the command output to a file instead of stdout")]
    public string OutputFile { get; set; } = "";

    [Option('m', "max-age-maintained", Required = false, Default = 30 * 18,
        HelpText = "maximum age of versions for still-maintained projects, 0 to disable")]
    public int MaxAgeMaintained { get; set; }

    [Option('u', "max-age-unmaintained", Required = false, Default = 30 * 48,
        HelpText = "maximum age of versions for unmaintained projects, 0 to disable")]
    public int MaxAgeUnmaintained { get; set; }

    [Option('v', "max-out-of-date-versions", Required = false, Default = 6,
        HelpText = "maximum number of versions out of date, 0 to disable")]
    public int MaxOutOfDateVersions { get; set; }

    [Option('r', "filter", Required = false, Default = null,
        HelpText = "filter versions by regular expression")]
    public string? Filter { get; set; }

    [Value(0, Required = true,
        HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }
}