// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    using Extensions.DependencyInjection;
    using Lib;
    using System.Net.Http;

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

        public DownloadTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        static async Task<int> Main(string[] args)
        {
            ShowToolBanner();

            // Setup our DI for the HTTP Client.
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddHttpClient()
                .BuildServiceProvider();

            // Get the IHttpClientFactory
            IHttpClientFactory httpClientFactory = serviceProvider.GetService<IHttpClientFactory>() ?? throw new InvalidOperationException();

            var downloadTool = new DownloadTool(httpClientFactory);
            var opts = downloadTool.ParseOptions<Options>(args);
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
                foreach (var target in targetList)
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        string downloadDirectory = options.DownloadDirectory == "." ? System.IO.Directory.GetCurrentDirectory() : options.DownloadDirectory;
                        var useCache = options.UseCache;
                        var packageDownloader = new PackageDownloader(purl, HttpClientFactory, downloadDirectory, useCache);

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