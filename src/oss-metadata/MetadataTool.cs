// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    using Extensions.DependencyInjection;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageManagers;
    using System.Net.Http;

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

            [Option('o', "output-file", Required = false, Default = "",
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; } = "";

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string> Targets { get => targets; set => targets = value; }

            private IEnumerable<string> targets = Array.Empty<string>();
        }

        public MetadataTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        private static async Task<PackageMetadata?> GetPackageMetadata(PackageURL purl, IHttpClientFactory httpClientFactory)
        {
            PackageMetadata? metadata = null;
            try
            {
                // Use reflection to find the correct downloader class
                BaseProjectManager? projectManager = ProjectManagerFactory.CreateProjectManager(purl, httpClientFactory);
                if (projectManager != null)
                {
                    metadata = await projectManager.GetPackageMetadata(purl);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error identifying source repository for {0}: {1}", purl, ex.Message);
            }

            return metadata;
        }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        private static async Task Main(string[] args)
        {
            ShowToolBanner();

            // Setup our DI for the HTTP Client.
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddHttpClient()
                .BuildServiceProvider();

            // Get the IHttpClientFactory
            IHttpClientFactory httpClientFactory = serviceProvider.GetService<IHttpClientFactory>() ?? throw new InvalidOperationException();

            MetadataTool? metadataTool = new MetadataTool(httpClientFactory);
            await metadataTool.ParseOptions<Options>(args).WithParsedAsync(metadataTool.RunAsync);
        }

        private async Task RunAsync(Options options)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            PackageMetadata? metadata = null;
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL? purl = new PackageURL(target);
                        metadata = await GetPackageMetadata(purl, HttpClientFactory);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
                Console.WriteLine(metadata?.ToString());
            }

            RestoreOutput();
        }
    }
}