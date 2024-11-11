// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("find-domain-squats", HelpText = "Run find-domain-squats tool")]
public class FindDomainSquatsToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Find Squat Candidates for the Given Packages",
                    new FindDomainSquatsToolOptions { Targets = new List<string>() {"[options]", "domains" } })};
        }
    }

    [Option('o', "output-file", Required = false, Default = "",
        HelpText = "send the command output to a file instead of stdout")]
    public string OutputFile { get; set; } = "";

    [Option('f', "format", Required = false, Default = "text",
        HelpText = "specify the output format(text|sarifv1|sarifv2)")]
    public string Format { get; set; } = "text";

    [Option('q', "quiet", Required = false, Default = false,
        HelpText = "Suppress console output.")]
    public bool Quiet { get; set; } = false;

    [Option('s', "sleep-delay", Required = false, Default = 0, HelpText = "Number of ms to sleep between checks.")]
    public int SleepDelay { get; set; }

    [Option('u', "unregistered", Required = false, Default = false, HelpText = "Don't show registered domains.")]
    public bool Unregistered { get; set; }

    [Option('r', "registered", Required = false, Default = false, HelpText = "Don't show unregistered domains.")]
    public bool Registered { get; set; }

    [Value(0, Required = true,
        HelpText = "Domain(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }
}
