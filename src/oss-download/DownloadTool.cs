// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    using PackageManagers;
    using PackageUrl;
    using System.Text.RegularExpressions;

    public class DownloadTool : OSSGadget
    {
        public enum ErrorCode
        {
            Ok,
            ProcessingException,
            NoTargets,
            ErrorParsingOptions
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

        public DownloadTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public DownloadTool() : this(new ProjectManagerFactory()) { }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        static async Task<int> Main(string[] args)
        {
            ShowToolBanner();

            DownloadTool? downloadTool = new DownloadTool();
            ParserResult<Options>? opts = downloadTool.ParseOptions<Options>(args);
            if (opts.Value is null)
            {
                if (opts.Errors.All(x => x.Tag == ErrorType.HelpRequestedError))
                {
                    return (int)ErrorCode.Ok;
                }
                else
                {
                    return (int)ErrorCode.ErrorParsingOptions;
                }
            }
            else
            {
                return (int)(await downloadTool.RunAsync(opts.Value));
            }
        }
        
        private async Task<ErrorCode> RunAsync(Options options)
        {
            if (options.Targets is IEnumerable<string> targetList && targetList.Any())
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        // PackageURL requires the @ in a namespace declaration to be escaped
                        // We find if the namespace contains an @ in the namespace
                        // And replace it with %40
                        string escapedNameSpaceTarget = EscapeAtSymbolInNameSpace(target);
                        PackageURL? purl = new PackageURL(escapedNameSpaceTarget);
                        string downloadDirectory = options.DownloadDirectory == "." ? System.IO.Directory.GetCurrentDirectory() : options.DownloadDirectory;
                        bool useCache = options.UseCache;
                        PackageDownloader? packageDownloader = new PackageDownloader(purl, ProjectManagerFactory, downloadDirectory, useCache);

                        List<string>? downloadResults = await packageDownloader.DownloadPackageLocalCopy(purl, options.DownloadMetadataOnly, options.Extract);
                        foreach (string? downloadPath in downloadResults)
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