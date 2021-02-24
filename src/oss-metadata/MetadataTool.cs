using CommandLine;
using CommandLine.Text;
using Microsoft.CST.OpenSource.Model;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
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

            private IEnumerable<string> targets;
        }

        public MetadataTool() : base()
        {
        }

        /// <summary>
        /// Get metadata for a package
        /// </summary>
        /// <param name="purl"></param>
        /// <returns></returns>
        public static async Task<PackageMetadata> GetPackageMetadata(PackageURL purl)
        {
            PackageMetadata metadata = null;
            try
            {
                // Use reflection to find the correct downloader class
                var projectManager = ProjectManagerFactory.CreateProjectManager(purl, null);
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
            var metadataTool = new MetadataTool();
            await metadataTool.ParseOptions<Options>(args).WithParsedAsync(metadataTool.RunAsync);
        }

        /// <summary>
        /// Entrypoint to run Metadata Tool
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        internal async Task RunAsync(Options options)
        {
            // select output destination and format
            PackageMetadata metadata = null;
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        metadata = await GetPackageMetadata(purl);
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