// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("defog", HelpText = "Identify hidden strings")]
public class DefogToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Identify hidden strings",
                    new DefogToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }

    [Option('d', "download-directory", Required = false, Default = ".",
        HelpText = "the directory to download the package to.")]
    public string DownloadDirectory { get; set; } = ".";
    
    [Option('c', "use-cache", Required = false, Default = false,
        HelpText = "do not download the package if it is already present in the destination directory.")]
    public bool UseCache { get; set; }

    [Option('f', "format", Required = false, Default = "text",
        HelpText = "specify the output format(text|sarifv1|sarifv2)")]
    public string Format { get; set; } = "text";

    [Option("save-found-binaries-to", Required = false, Default = "",
        HelpText = "location to save defogged binaries")]
    public string SaveFoundBinariesLocation { get; set; } = string.Empty;
    
    [Option("save-archives-to", Required = false, Default = "",
        HelpText = "location to save defogged archives")]
    public string SaveFoundArchivesTo { get; set; } = string.Empty;
    
    [Option("save-blobs-to", Required = false, Default = "",
        HelpText = "location to save defogged archives")]
    public string SaveFoundBlobsTo { get; set; } = string.Empty;
    
    [Option("report-blobs", Required = false, Default = false,
        HelpText = "if blobs should be reported")]
    public bool ShouldSaveBlobs { get; set; } = false;
    
    [Option("minimum-hex-length", Required = false, Default = 0,
        HelpText = "location to save defogged archives")]
    public int MinimumHexLength { get; set; } = 0;
    
    [Option("minimum-base64-length", Required = false, Default = 0,
        HelpText = "location to save defogged archives")]
    public int MinimumBase64Length { get; set; } = 0;

    [Value(0, Required = true,
        HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }
}