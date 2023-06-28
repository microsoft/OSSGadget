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
    using Contracts;
    using Microsoft.CST.OpenSource.PackageManagers;
    using PackageUrl;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class HealthTool : OSSGadget
    {
        
        public HealthTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public HealthTool() : this(new ProjectManagerFactory())
        {
        }
        public async Task<HealthMetrics?> CheckHealth(PackageURL purl)
        {
            IBaseProjectManager? packageManager = ProjectManagerFactory.CreateProjectManager(purl);

            if (packageManager != null)
            {
                string? content = await packageManager.GetMetadataAsync(purl);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    RepoSearch repoSearcher = new RepoSearch(ProjectManagerFactory);
                    foreach ((PackageURL githubPurl, double _) in await repoSearcher.ResolvePackageLibraryAsync(purl))
                    {
                        try
                        {
                            GitHubHealthAlgorithm? healthAlgorithm = new GitHubHealthAlgorithm(githubPurl);
                            HealthMetrics? health = await healthAlgorithm.GetHealth();
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
                throw new ArgumentException($"Invalid Package URL type: {purl.Type}");
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
                            HelpText = "specify the output format(text|sarifv1|sarifv2)")]
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
            ShowToolBanner();
            HealthTool? healthTool = new HealthTool();
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
                foreach (string target in targetList)
                {
                    try
                    {
                        // PackageURL requires the @ in a namespace declaration to be escaped
                        // We find if the namespace contains an @ in the namespace
                        // And replace it with %40
                        string escapedNameSpaceTarget = EscapeAtSymbolInNameSpace(target);
                        PackageURL? purl = new PackageURL(escapedNameSpaceTarget);
                        HealthMetrics? healthMetrics = CheckHealth(purl).Result;
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