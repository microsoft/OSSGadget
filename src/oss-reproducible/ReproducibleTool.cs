// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using System.Security.Cryptography;
using CommandLine.Text;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.CST.RecursiveExtractor;
using System.Runtime.InteropServices;
using Microsoft.CST.OpenSource.Reproducibility;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading;

namespace Microsoft.CST.OpenSource
{
    public class ReproducibleToolResult
    {
        public bool IsReproducible { get; set; } = false;
        public List<StrategyResult>? Results { get; set; }
    }

    public class ReproducibleTool : OSSGadget
    {
        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Checks reproducibility of the given package",
                        new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

            [Value(0, Required = true, HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }

            [Option('a', "all-strategies", Required = false, Default = false,
                HelpText = "Execute all strategies, even after a successful one is identified.")]
            public bool AllStrategies { get; set; }

            [Option('s', "source-ref", Required = false, Default = "",
                HelpText = "If a source version cannot be identified, use the specified git reference (tag, commit, etc.).")]
            public string OverrideSourceReference { get; set; } = "";

            [Option('o', "output-file", Required = false, Default = "", HelpText = "Send the command output to a file instead of standard output")]
            public string OutputFile { get; set; } = "";

            [Option('l', "leave-intermediate", Required = false, Default = false,
                HelpText = "Do not clean up intermediate files (useful for debugging).")]
            public bool LeaveIntermediateFiles { get; set; }

        }

        public ReproducibleTool() : base()
        {
        }

        /// <summary>
        ///     Main entrypoint for the download program.
        /// </summary>
        /// <param name="args"> parameters passed in from the user </param>
        public static async Task Main(string[] args)
        {
            ShowToolBanner();
            Console.WriteLine();
            var reproducibleTool = new ReproducibleTool();
            await reproducibleTool.ParseOptions<Options>(args).WithParsedAsync(reproducibleTool.RunAsync);
        }

        private async Task RunAsync(Options options)
        {
            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        Console.WriteLine($"Analyzing: {target}...");
                        Logger.Debug("Processing: {0}", target);

                        var purl = new PackageURL(target);
                        if (purl.Version == null)
                        {
                            Logger.Error("Package is missing a version, which is required for this tool.");
                            continue;
                        }
                        var tempDirectoryName = Guid.NewGuid().ToString();
                        if (Directory.Exists(tempDirectoryName))
                        {
                            Directory.Delete(tempDirectoryName, true);  // Just in case
                        }

                        // Download the package
                        Console.WriteLine("Downloading...");
                        var packageDownloader = new PackageDownloader(purl, Path.Join(tempDirectoryName, "package"));
                        var downloadResults = await packageDownloader.DownloadPackageLocalCopy(purl, false, true);

                        if (!downloadResults.Any())
                        {
                            Logger.Error("Unable to download package.");
                        }

                        // Locate the source
                        Console.WriteLine("Locating source...");
                        var findSourceTool = new FindSourceTool();
                        var sourceMap = await findSourceTool.FindSourceAsync(purl);
                        if (!sourceMap.Any())
                        {
                            Logger.Warn("Unable to locate source. Trying 'master'");
                            continue;
                        }
                        var sourceMapList = sourceMap.ToList();
                        sourceMapList.Sort((a, b) => a.Value.CompareTo(b.Value));
                        var bestSourcePurl = sourceMapList.Last().Key;
                        if (string.IsNullOrEmpty(bestSourcePurl.Version))
                        {
                            // Tie back the original version to the new PackageURL
                            bestSourcePurl = new PackageURL(bestSourcePurl.Type, bestSourcePurl.Namespace, bestSourcePurl.Name,
                                                            purl.Version, bestSourcePurl.Qualifiers, bestSourcePurl.Subpath);
                        }
                        Logger.Debug("Identified best source code repository: {0}", bestSourcePurl);

                        // Download the source
                        Console.WriteLine("Downloading source...");
                        foreach (var reference in new[] { bestSourcePurl.Version, options.OverrideSourceReference, "master", "main" })
                        {
                            if (string.IsNullOrWhiteSpace(reference))
                            {
                                continue;
                            }
                            Logger.Debug("Trying to download package, version/reference [{0}].", reference);
                            var purlRef = new PackageURL(bestSourcePurl.Type, bestSourcePurl.Namespace, bestSourcePurl.Name, reference, bestSourcePurl.Qualifiers, bestSourcePurl.Subpath);
                            packageDownloader = new PackageDownloader(purlRef, Path.Join(tempDirectoryName, "src"));
                            downloadResults = await packageDownloader.DownloadPackageLocalCopy(purlRef, false, true);
                            if (downloadResults.Any())
                            {
                                break;
                            }
                        }
                        if (!downloadResults.Any())
                        {
                            Logger.Error("Unable to download source.");
                            continue;
                        }

                        // Execute all available strategies
                        var strategyOptions = new StrategyOptions()
                        {
                            PackageDirectory = Path.Join(tempDirectoryName, "package"),
                            SourceDirectory = Path.Join(tempDirectoryName, "src"),
                            PackageUrl = purl,
                            TemporaryDirectory = Path.GetFullPath(tempDirectoryName)
                        };

                        // First, check to see how many strategies apply
                        var strategies = BaseStrategy.GetStrategies(strategyOptions) ?? Array.Empty<Type>();

                        int numStrategiesApplies = 0;
                        foreach (var strategy in strategies)
                        {
                            var ctor = strategy.GetConstructor(new Type[] { typeof(StrategyOptions) });
                            if (ctor != null)
                            {
                                var strategyObject = (BaseStrategy)(ctor.Invoke(new object?[] { strategyOptions }));
                                if (strategyObject.StrategyApplies())
                                { 
                                    numStrategiesApplies++;
                                }
                            }
                        }
                        
                        Console.Write("Out of {0} potential strategies, {1} apply. ", strategies.Count(), numStrategiesApplies);
                        if (options.AllStrategies)
                        {
                            Console.WriteLine("Analysis will continue even after a successful strategy is found.");
                        }
                        else
                        {
                            Console.WriteLine("Analysis will stop after the first successful strategy is found.");
                        }
                        List<StrategyResult> strategyResults = new List<StrategyResult>();

                        bool overallStrategyResult = false;

                        Console.WriteLine();
                        Console.WriteLine("Results:");

                        foreach (var strategy in strategies)
                        {
                            var ctor = strategy.GetConstructor(new Type[] { typeof(StrategyOptions) });
                            if (ctor != null)
                            {
                                // Create a temporary directory, copy the contents from source/package so that
                                // this strategy can modify the contents without affecting other strategies.
                                var tempStrategyOptions = new StrategyOptions
                                {
                                    PackageDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name, "package"),
                                    SourceDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name, "src"),
                                    TemporaryDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name),
                                    PackageUrl = strategyOptions.PackageUrl
                                };

                                try
                                {
                                    Helpers.DirectoryCopy(strategyOptions.PackageDirectory, tempStrategyOptions.PackageDirectory);
                                    Helpers.DirectoryCopy(strategyOptions.SourceDirectory, tempStrategyOptions.SourceDirectory);
                                }
                                catch(Exception ex)
                                {
                                    Logger.Warn(ex, "Error copying directory for strategy. Aborting execution.");
                                    continue;
                                }
                                
                                try
                                {
                                    var strategyObject = (BaseStrategy)(ctor.Invoke(new object?[] { tempStrategyOptions }));
                                    StrategyResult? strategyResult = strategyObject.Execute();
                                    if (strategyResult != null)
                                    {
                                        strategyResults.Add(strategyResult);
                                        overallStrategyResult |= strategyResult.IsSuccess;
                                    }

                                    if (strategyResult != null)
                                    {
                                        if (strategyResult.IsSuccess)
                                        {
                                            Console.WriteLine($"  [✓] {strategy.Name}");
                                            if (!options.AllStrategies)
                                            {
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"  [✗] {strategy.Name}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  [-] {strategy.Name}");
                                    }
                                }
                                catch(Exception ex)
                                {
                                    Logger.Warn(ex, "Error processing {0}: {1}", strategy, ex.Message);
                                    Logger.Debug(ex.StackTrace);
                                }
                            }
                        }

                        Console.WriteLine("\nSummary:");
                        if (overallStrategyResult)
                        {
                            Console.WriteLine($"  [✓] Yes, this package is reproducible.");
                        }
                        else
                        {
                            Console.WriteLine($"  [✗] No, this package is not reproducible.");
                        }

                        var finalResult = new ReproducibleToolResult
                        {
                            IsReproducible = overallStrategyResult,
                            Results = strategyResults
                        };

                        // Write the output somewhere
                        var jsonResults = JsonSerializer.Serialize<ReproducibleToolResult>(finalResult, new JsonSerializerOptions { WriteIndented = true });
                        if (!string.IsNullOrWhiteSpace(options.OutputFile) && !string.Equals(options.OutputFile, "-", StringComparison.InvariantCultureIgnoreCase))
                        {
                            try
                            {
                                File.WriteAllText(options.OutputFile, jsonResults);
                                Console.WriteLine($"Detailed results are available in {options.OutputFile}.");
                            }
                            catch(Exception ex)
                            {
                                Logger.Warn(ex, "Unable to write to {0}. Writing to console instead.", options.OutputFile);
                                Console.Error.WriteLine(jsonResults);
                            }
                        }
                        else if (string.Equals(options.OutputFile, "-", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Console.Error.WriteLine(jsonResults);
                        }

                        if (options.LeaveIntermediateFiles)
                        {
                            Console.WriteLine($"Intermediate files are located in [{tempDirectoryName}].");
                        }
                        else
                        {
                            // Clean up our temporary directory
                            int numCleanTries = 2;
                            while (numCleanTries-- >= 0)
                            {
                                try
                                {
                                    Directory.Delete(tempDirectoryName, true);
                                    break;
                                }
                                catch (Exception)
                                {
                                    Logger.Debug("Error deleting {0}, sleeping for 5 seconds.", tempDirectoryName);
                                    Thread.Sleep(5000);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                        Logger.Debug(ex.StackTrace);
                    }
                }
            }
        }
    }
}