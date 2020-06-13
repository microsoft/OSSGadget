// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class DetectBackdoorTool : OSSGadget
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-detect-backdoor";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(DetectBackdoorTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>
        /// Location of the backdoor detection rules.
        /// </summary>
        private const string RULE_DIRECTORY = @"Resources\BackdoorRules";

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        public class Options
        {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable - commandlineparser doesnt handle nullable fields
            [Option('d', "download-directory", Required = false, Default = null, 
                
                HelpText = "the directory to download the package to.")]
            public string DownloadDirectory { get; set; }

            [Option('c', "use-cache", Required = false, Default = false, HelpText = "do not download the package if it is already present in the destination directory.")]
            public bool UseCache { get; set; }

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
                        new Example("Find the characterstics for the given package", 
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }
        }


        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        static async Task Main(string[] args)
        {
            var detectBackdoorTool = new DetectBackdoorTool();
            await detectBackdoorTool.ParseOptions<Options>(args).WithParsedAsync(detectBackdoorTool.RunAsync);
        }

        async Task RunAsync(Options options)
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
                        characteristicTool.AnalyzePackage(cOptions, purl, 
                            (string?)options.DownloadDirectory, 
                            (bool?)options.UseCache == true).Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger?.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
        }

        public DetectBackdoorTool() : base()
        {
        }
    }
}
