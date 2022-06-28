// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using DevLab.JmesPath;

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
                        new Example("Find the normalized metadata for the given package. Not all package ecosystems are supported.",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
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
        private bool _ShowError = false;

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
            var parserResult = await metadataTool.ParseOptions<Options>(args).WithParsedAsync(async options => {
                if (options.ListSupported)
                {
                    metadataTool.ListSupported(options);
                    return;
                }
                await metadataTool.RunAsync(options);
            });
        }

        private void ListSupported(Options options)
        {
            Console.WriteLine("\nSupported ecosystems:");
            var dataSources = typeof(BaseMetadataSource).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(BaseMetadataSource)));

            foreach (var dataSource in dataSources.Where(d => d != null))
            {
                var field = dataSource.GetField("VALID_TYPES", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (field == null)
                    continue;

                var validTypes = (List<string>?)field.GetValue(dataSource.GetConstructor(new Type[] { })?.Invoke(new object[] { }));
                if (validTypes == null || validTypes.Count == 0)
                    continue;

                Console.WriteLine($"  {dataSource.Name}");
                validTypes.Sort();

                foreach (var validType in validTypes)
                {
                    Console.WriteLine($"  - {validType}");
                }
                Console.WriteLine();
            }
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
                else if (options.DataSource.Equals("native", StringComparison.InvariantCultureIgnoreCase))
                    metadataSource = new NativeMetadataSource();
                else
                    throw new ArgumentException($"Unknown data source: {options.DataSource}");

                var output = new List<object>();

                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL purl = new(target);
                        Logger.Info("Collecting metadata for {0}", purl);
                        JsonDocument? metadata = await metadataSource.GetMetadataForPackageUrlAsync(purl, options.UseCache);
                        if (metadata != null)
                        {
                            if (options.JmesPathExpression != null)
                            {
                                Logger.Debug("Running JMESPath expression [{0}] against metadata.", options.JmesPathExpression);
                                var metadataString = JsonSerializer.Serialize(metadata);
                                var result = new JmesPath().Transform(metadataString, options.JmesPathExpression);
                                if (result != null && !string.Equals(result, "null", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    output.Add(JsonDocument.Parse(result).RootElement);
                                }
                                else
                                {
                                    Logger.Info("No results found JMESPath expression [{0}] against target {1}", options.JmesPathExpression, target);
                                }
                            }
                            else
                            {
                                output.Add(metadata);
                            }
                        }
                        else
                        {
                            Logger.Warn("No metadata found for {0}", target);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
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
                Logger.Error("No targets specified. Use --help for options.");
            }
        }
    }
}