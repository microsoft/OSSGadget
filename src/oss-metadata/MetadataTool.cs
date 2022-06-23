// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

namespace Microsoft.CST.OpenSource
{
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageManagers;
    using PackageUrl;
    using System.IO;

    public class MetadataTool : OSSGadget
    {
        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find the normalized metadata for the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }
            [Option('s', "data-source", Required = false, Default="deps.dev", 
                HelpText = "The data source to use (deps.dev, libraries.io)")]
            public string DataSource { get; set; } = "deps.dev";
            
            [Option('c', "useCache", Required = false, Default = false,
                HelpText = "Should metadata use the cache, and get cached?")]
            public bool UseCache { get; set; } = false;

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string> Targets { get => targets; set => targets = value; }

            private IEnumerable<string> targets = Array.Empty<string>();
        }

        public MetadataTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public MetadataTool(): this(new ProjectManagerFactory()) { }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        private static async Task Main(string[] args)
        {
            ShowToolBanner();
            MetadataTool? metadataTool = new MetadataTool();
            await metadataTool.ParseOptions<Options>(args).WithParsedAsync(metadataTool.RunAsync);
        }

        private async Task RunAsync(Options options)
        {
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                BaseMetadataSource? metadataSource = null;
                
                if (options.DataSource.Equals("deps.dev", StringComparison.InvariantCultureIgnoreCase))
                    metadataSource = new DepsDevMetadataSource();
                else if (options.DataSource.Equals("libraries.io", StringComparison.InvariantCultureIgnoreCase))
                    metadataSource = new LibrariesIoMetadataSource();
                else
                    throw new ArgumentException($"Unknown data source: {options.DataSource}");

                var output = new List<JsonDocument>();

                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL purl = new(target);
                        Logger.Info("Collecting metadata for {0}", purl);
                        JsonDocument? metadata = await metadataSource.GetMetadataForPackageUrlAsync(purl, options.UseCache);
                        if (metadata != null)
                        {
                            output.Add(metadata);
                        }
                        else
                        {
                            Logger.Warn("No metadata found for {0}", target);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }
                if (output.Count == 1)
                {
                    Console.WriteLine(JsonSerializer.Serialize(output.First(), new JsonSerializerOptions { WriteIndented = true }));
                } 
                else if (output.Count > 1)
                {
                    Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            else
            {
                Logger.Error("No targets specified");
            }
        }
    }
}