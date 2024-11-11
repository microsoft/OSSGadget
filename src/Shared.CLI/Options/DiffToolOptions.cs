// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;

[Verb("diff", HelpText = "Run diff tool")]
public class DiffToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Diff the given packages",
                new DiffToolOptions { Targets = new List<string>() {"[options]", "package-url package-url-2" } })};
        }
    }

    [Option('d', "download-directory", Required = false, Default = null,
                    HelpText = "the directory to download the packages to.")]
    public string? DownloadDirectory { get; set; } = null;

    [Option('c', "use-cache", Required = false, Default = false,
        HelpText = "Do not download the package if it is already present in the destination directory and do not delete the package after processing.")]
    public bool UseCache { get; set; }

    [Option('w', "crawl-archives", Required = false, Default = true,
        HelpText = "Crawl into archives found in packages.")]
    public bool CrawlArchives { get; set; }

    [Option('B', "context-before", Required = false, Default = 0,
        HelpText = "Number of previous lines to give as context.")]
    public int Before { get; set; } = 0;

    [Option('A', "context-after", Required = false, Default = 0,
        HelpText = "Number of subsequent lines to give as context.")]
    public int After { get; set; } = 0;

    [Option('C', "context", Required = false, Default = 0,
        HelpText = "Number of lines to give as context. Overwrites Before and After options. -1 to print all.")]
    public int Context { get; set; } = 0;

    [Option('a', "added-only", Required = false, Default = false,
        HelpText = "Only show added lines (and requested context).")]
    public bool AddedOnly { get; set; } = false;

    [Option('r', "removed-only", Required = false, Default = false,
        HelpText = "Only show removed lines (and requested context).")]
    public bool RemovedOnly { get; set; } = false;

    [Option('f', "format", Required = false, Default = "text",
        HelpText = "Choose output format. (text|sarifv1|sarifv2)")]
    public string Format { get; set; } = "text";

    [Option('o', "output-location", Required = false, Default = null,
        HelpText = "Output location. Don't specify for console output.")]
    public string? OutputLocation { get; set; } = null;

    [Value(0, Required = true,
        HelpText = "Exactly two Filenames or PackgeURL specifiers to analyze.", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string> Targets { get; set; } = Array.Empty<string>();
}