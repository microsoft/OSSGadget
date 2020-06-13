// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.CST.OpenSource.Health;
using Microsoft.CodeAnalysis.Sarif;
using CommandLine;
using CommandLine.Text;
using static Microsoft.CST.OpenSource.Shared.OutputBuilder;

namespace Microsoft.CST.OpenSource
{
    public class HealthTool : OSSGadget
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-health";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(HealthTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        public class Options
        {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable - commandlineparser doesnt handle nullable fields
            [Option('f', "format", Required = false, Default = "text", 
                HelpText = "selct the output format(text|sarifv1|sarifv2)")]
            public string Format { get; set; }

            [Option('o', "output-file", Required = false, Default = null, 
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; }

            [Value(0, Required = true, 
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string> Targets { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find the source code repository for the given package", 
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }
        }

        static async Task Main(string[] args)
        {
            var healthTool = new HealthTool();
            await healthTool.ParseOptions<Options>(args).WithParsedAsync(healthTool.RunAsync);
        }

        async Task RunAsync(Options options)
        {
            // select output destination and format
            this.SelectOutput(options.OutputFile);
            OutputBuilder? outputBuilder = this.SelectFormat(options.Format);
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        var healthMetrics = this.CheckHealth(purl).Result;
                        this.AppendOutput(outputBuilder, purl, healthMetrics);

                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder?.PrintOutput();
            }
            this.RestoreOutput();
        }

        public HealthTool() : base()
        {
        }

        public async Task<HealthMetrics?> CheckHealth(PackageURL purl)
        {
            var packageManager = ProjectManagerFactory.CreateProjectManager(purl, null);

            if (packageManager != null)
            {
                var content = await packageManager.GetMetadata(purl);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    RepoSearch repoSearcher = new RepoSearch();
                    foreach (var (githubPurl, _) in await repoSearcher.ResolvePackageLibraryAsync(purl))
                    {
                        try
                        {
                            var healthAlgorithm = new GitHubHealthAlgorithm(githubPurl);
                            var health = await healthAlgorithm.GetHealth();
                            return health;
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "Unable to calculate health for {0}: {1}", githubPurl, ex.Message);
                        }
                    }
                }
                else
                {
                    Logger.Warn("No metadata found for {0}", purl.ToString());
                }
            }
            else
            {
                throw new ArgumentException("Invalid Package URL type: {0}", purl.Type);
            }
            return null;
        }

        void AppendOutput(OutputBuilder? outputBuilder, PackageURL purl, HealthMetrics? healthMetrics)
        {
            switch (outputBuilder?.currentOutputFormat ?? OutputFormat.text)
            {
                case OutputFormat.text:
                    outputBuilder?.AppendOutput(new List<string>() { 
                        $"Health for {purl} (via {purl})", 
                        healthMetrics?.ToString() ?? string.Empty 
                    });
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder?.AppendOutput(healthMetrics?.toSarif() ?? Array.Empty<Result>().ToList());
                    break;

                default:
                    outputBuilder?.AppendOutput($"Health for {purl} (via {purl})\n");
                    outputBuilder?.AppendOutput(healthMetrics?.ToString() ?? string.Empty);
                    break;
            }
        }
    }
}
