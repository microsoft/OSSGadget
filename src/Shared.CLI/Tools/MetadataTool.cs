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
    using OssGadget.Options;
    using PackageUrl;
    using System.IO;

    public class MetadataTool : BaseTool<MetadataToolOptions>
    {
        private bool _ShowError = false;

        public MetadataTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public MetadataTool(): this(new ProjectManagerFactory()) { }

        private void ListSupported(MetadataToolOptions metadataToolOptions)
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

        public override async Task<ErrorCode> RunAsync(MetadataToolOptions metadataToolOptions)
        {
            if (metadataToolOptions.ListSupported)
            {
                ListSupported(metadataToolOptions);
                return ErrorCode.Ok;
            }
            if (metadataToolOptions.Targets is IList<string> targetList && targetList.Count > 0)
            {
                BaseMetadataSource? metadataSource = null;
                
                if (metadataToolOptions.DataSource.Equals("deps.dev", StringComparison.InvariantCultureIgnoreCase))
                    metadataSource = new DepsDevMetadataSource();
                else if (metadataToolOptions.DataSource.Equals("libraries.io", StringComparison.InvariantCultureIgnoreCase))
                    metadataSource = new LibrariesIoMetadataSource();
                else if (metadataToolOptions.DataSource.Equals("native", StringComparison.InvariantCultureIgnoreCase))
                    metadataSource = new NativeMetadataSource();
                else
                    throw new ArgumentException($"Unknown data source: {metadataToolOptions.DataSource}");

                var output = new List<object>();

                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL purl = new(target);
                        Logger.Info("Collecting metadata for {0}", purl);
                        JsonDocument? metadata = await metadataSource.GetMetadataForPackageUrlAsync(purl, metadataToolOptions.UseCache);
                        if (metadata != null)
                        {
                            if (metadataToolOptions.JmesPathExpression != null)
                            {
                                Logger.Debug("Running JMESPath expression [{0}] against metadata.", metadataToolOptions.JmesPathExpression);
                                var metadataString = JsonSerializer.Serialize(metadata);
                                var result = new JmesPath().Transform(metadataString, metadataToolOptions.JmesPathExpression);
                                if (result != null && !string.Equals(result, "null", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    output.Add(JsonDocument.Parse(result).RootElement);
                                }
                                else
                                {
                                    Logger.Info("No results found JMESPath expression [{0}] against target {1}", metadataToolOptions.JmesPathExpression, target);
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
                return ErrorCode.NoTargets;
            }

            return ErrorCode.Ok;
        }
    }
}