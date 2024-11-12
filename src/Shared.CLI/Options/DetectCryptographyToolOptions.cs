// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

[Verb("detect-cryptography", HelpText = "Run detect crypto tool")]
public class DetectCryptographyToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Identify potential malware or backdoors in the given package",
                    new DetectCryptographyToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }

    [Value(0, Required = true,
        HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string>? Targets { get; set; }

    [Option('c', "use-cache", Required = false, Default = false,
        HelpText = "do not download the package if it is already present in the destination directory.")]
    public bool UseCache { get; set; }
    
    [Option('d', "download-directory", Required = false, Default = ".",
        HelpText = "the directory to download the package to.")]
    public string DownloadDirectory { get; set; } = ".";
    
    [Option("disable-default-rules", Required = false, Default = false,
        HelpText = "Igonre default rules")]
    public bool DisableDefaultRules { get; set; } = false;
    
    [Option("custom-rule-directory", Required = false, Default = "",
        HelpText = "Location with custom rules")]
    public string CustomRuleDirectory { get; set; } = string.Empty;
    
    [Option("verbose", Required = false, Default = false,
        HelpText = "Increase verbosity")]
    public bool Verbose { get; set; } = false;
    
    [Option("output-file", Required = false, Default = "",
        HelpText = "the file to write output to")]
    public string OutputFile { get; set; } = string.Empty;
    
    [Option('f', "format", Required = false, Default = "text",
        HelpText = "specify the output format(text|sarifv1|sarifv2)")]
    public string Format { get; set; } = "text";
}