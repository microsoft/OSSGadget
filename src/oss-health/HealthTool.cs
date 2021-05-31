// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.CST.OpenSource.Health;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

namespace Microsoft.CST.OpenSource
{
    public class HealthTool : OSSGadget
    {
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

        public class Options
        {
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

            [Option('f', "format", Required = false, Default = "text",
                            HelpText = "selct the output format(text|sarifv1|sarifv2)")]
            public string? Format { get; set; }

            [Option('o', "output-file", Required = false, Default = "",
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; } = "";

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }
        }

        private static async Task Main(string[] args)
        {
            await ShowToolBanner();
            var healthTool = new HealthTool();
            await healthTool.ParseOptions<Options>(args).WithParsedAsync(healthTool.RunAsync);
        }

        private void AppendOutput(IOutputBuilder outputBuilder, PackageURL purl, HealthMetrics healthMetrics)
        {
            switch (currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    outputBuilder.AppendOutput(new List<string>() {
                        $"Health for {purl} (via {purl})",
                        healthMetrics.ToString()
                    });
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(healthMetrics.toSarif());
                    break;
            }
        }

        private async Task RunAsync(Options options)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(options.Format ?? OutputFormat.text.ToString());
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        var healthMetrics = CheckHealth(purl).Result;
                        if (healthMetrics == null)
                        {
                            Logger.Debug($"Cannot compute Health for {purl}");
                        }
                        else
                        {
                            AppendOutput(outputBuilder, purl, healthMetrics);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            }
            RestoreOutput();
        }
    }
}