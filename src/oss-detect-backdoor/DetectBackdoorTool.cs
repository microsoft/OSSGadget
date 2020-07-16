// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    public class DetectBackdoorTool : OSSGadget
    {
        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find the characterstics for the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

            [Option('d', "download-directory", Required = false, Default = ".",
                            HelpText = "the directory to download the package to.")]
            public string DownloadDirectory { get; set; } = ".";

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }

            [Option('c', "use-cache", Required = false, Default = false,
                HelpText = "do not download the package if it is already present in the destination directory.")]
            public bool UseCache { get; set; }
        }

        public DetectBackdoorTool() : base()
        {
        }

        /// <summary>
        ///     Location of the backdoor detection rules.
        /// </summary>
        private const string RULE_DIRECTORY = @"Resources\BackdoorRules";

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        private static async Task Main(string[] args)
        {
            var detectBackdoorTool = new DetectBackdoorTool();
            await detectBackdoorTool.ParseOptions<Options>(args).WithParsedAsync(detectBackdoorTool.RunAsync);
        }

        private async Task RunAsync(Options options)
        {
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                var characteristicTool = new CharacteristicTool();
                CharacteristicTool.Options cOptions = new CharacteristicTool.Options();
                cOptions.Targets = options.Targets;
                cOptions.DisableDefaultRules = true;
                cOptions.CustomRuleDirectory = RULE_DIRECTORY;
                cOptions.DownloadDirectory = options.DownloadDirectory;
                cOptions.UseCache = options.UseCache;

                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        string downloadDirectory = options.DownloadDirectory == "." ? Directory.GetCurrentDirectory() : options.DownloadDirectory;
                        characteristicTool.AnalyzePackage(cOptions, purl,
                            downloadDirectory,
                            options.UseCache == true).Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
        }
    }
}