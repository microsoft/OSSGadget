// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;

[Verb("metadata", HelpText = "Find the normalized metadata for the given package. Not all package ecosystems are supported.")]
public class MetadataToolOptions : BaseToolOptions
{
    [Usage()]
    public static IEnumerable<Example> Examples
    {
        get
        {
            return new List<Example>() {
                new Example("Find the normalized metadata for the given package. Not all package ecosystems are supported.",
                    new MetadataToolOptions { Targets = new List<string>() {"[options]", "package-url..." } })};
        }
    }
    [Option('s', "data-source", Required = false, Default="deps.dev", 
        HelpText = "The data source to use (deps.dev, libraries.io, or native)")]
    public string DataSource { get; set; } = "deps.dev";
            
    [Option('j', "jmes-path", Required = false, Default = null,
        HelpText = "The JMESPath expression to use to filter the data")]
    public string? JmesPathExpression { get; set; }

    [Option('c', "useCache", Required = false, Default = false,
        HelpText = "Should metadata use the cache, and get cached?")]
    public bool UseCache { get; set; } = false;

    [Option("list-supported", Required = false, Default = false,
        HelpText = "List the supported package ecosystems")]
    public bool ListSupported { get; set; } = false;

    [Value(0, Required = false, 
        HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
    public IEnumerable<string> Targets { get => targets; set => targets = value; }

    private IEnumerable<string> targets = Array.Empty<string>();
}