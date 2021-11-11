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
    public class DownloadTool : OSSGadget
    {
        public enum ErrorCode
        {
            Ok,
            ProcessingException,
            NoTargets
        }

        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Download the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

            [Option('x', "download-directory", Required = false, Default = ".",
                HelpText = "the directory to download the package to.")]
            public string DownloadDirectory { get; set; } = ".";

            [Option('m', "download-metadata-only", Required = false, Default = false,
                                                    HelpText = "download only the package metadata, not the package.")]
            public bool DownloadMetadataOnly { get; set; }

            [Option('e', "extract", Required = false, Default = false,
                HelpText = "Extract the package contents")]
            public bool Extract { get; set; }

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }

            [Option('c', "use-cache", Required = false, Default = false,
                HelpText = "do not download the package if it is already present in the destination directory.")]
            public bool UseCache { get; set; }
        }

        public DownloadTool() : base()
        {
        }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        static async Task<int> Main(string[] args)
        {
            ShowToolBanner();
            var downloadTool = new DownloadTool();
            var opts = downloadTool.ParseOptions<Options>(args).Value;
            return (int)(await downloadTool.RunAsync(opts));
        }

        private async Task<ErrorCode> RunAsync(Options options)
        {
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        string downloadDirectory = options.DownloadDirectory == "." ? Directory.GetCurrentDirectory() : options.DownloadDirectory;
                        var useCache = options.UseCache;
                        var packageDownloader = new PackageDownloader(purl, downloadDirectory, useCache);

                        var downloadResults = await packageDownloader.DownloadPackageLocalCopy(purl, options.DownloadMetadataOnly, options.Extract);
                        foreach (var downloadPath in downloadResults)
                        {
                            if (string.IsNullOrEmpty(downloadPath))
                            {
                                Logger.Error("Unable to download {0}.", purl.ToString());
                            }
                            else
                            {
                                Logger.Info("Downloaded {0} to {1}", purl.ToString(), downloadPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                        return ErrorCode.ProcessingException;
                    }
                }
            }
            else
            {
                Logger.Error("No targets were specified for downloading.");
                return ErrorCode.NoTargets;
            }
            return ErrorCode.Ok;
        }
    }
}