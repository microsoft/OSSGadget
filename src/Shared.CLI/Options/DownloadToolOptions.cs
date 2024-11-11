// Copyright (c) Microsoft Corporation. Licensed under the MIT License.
namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("download", HelpText = "Download a specified package by PackageUrl")]
public class DownloadToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Download the given package",
                    new DownloadToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }

    [Option('x', "download-directory", Required = false, Default = ".",
        HelpText = "the directory to download the package to.")]
    public string DownloadDirectory { get; set; } = ".";

    [Option('m', "download-metadata-only", Required = false, Default = false,
        HelpText = "download only the package metadata, not the package.")]
    public bool DownloadMetadataOnly { get; set; }

    [Option('e', "extract", Required = false, Default = false,
        HelpText = "Extract the package contents")]
    public bool Extract { get; set; }

    [Value(0, Required = true,
        HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }

    [Option('c', "use-cache", Required = false, Default = false,
        HelpText = "do not download the package if it is already present in the destination directory.")]
    public bool UseCache { get; set; }
}
