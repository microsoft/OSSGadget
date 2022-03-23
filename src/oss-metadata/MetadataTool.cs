// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    using Contracts;
    using Microsoft.CST.OpenSource.Model;
    using Microsoft.CST.OpenSource.PackageManagers;
    using Model.Providers;
    using PackageUrl;
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
            
            [Option('c', "useCache", Required = false, Default = false,
                HelpText = "Should metadata use the cache, and get cached?")]
            public bool UseCache { get; set; } = false;

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string> Targets { get => targets; set => targets = value; }

            private IEnumerable<string> targets = Array.Empty<string>();
        }

        public MetadataTool(IHttpClientFactory httpClientFactory, IManagerProvider<IManagerMetadata>? provider = null) : base(httpClientFactory, provider)
        {
        }

        public MetadataTool(): this(new DefaultHttpClientFactory()) { }

        private static async Task<PackageMetadata?> GetPackageMetadata(PackageURL purl, IHttpClientFactory httpClientFactory, IManagerProvider<IManagerMetadata>? provider = null, bool useCache = true)
        {
            PackageMetadata? metadata = null;
            try
            {
                // Use reflection to find the correct downloader class
                BaseProjectManager? projectManager = ProjectManagerFactory.CreateProjectManager(purl, httpClientFactory, provider);
                if (projectManager != null)
                {
                    metadata = await projectManager.GetPackageMetadata(purl, useCache);
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
            MetadataTool? metadataTool = new MetadataTool();
            await metadataTool.ParseOptions<Options>(args).WithParsedAsync(metadataTool.RunAsync);
        }

        private async Task RunAsync(Options options)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL purl = new(target);
                        Logger.Info($"Collecting metadata for {purl}");
                        PackageMetadata? metadata = await GetPackageMetadata(purl, HttpClientFactory, Provider, options.UseCache);
                        Logger.Info(metadata?.ToString());
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }

            RestoreOutput();
        }
    }
}