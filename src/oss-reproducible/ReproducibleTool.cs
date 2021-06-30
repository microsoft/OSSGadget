// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Reproducibility;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static Crayon.Output;

namespace Microsoft.CST.OpenSource
{
    public enum DiffTechnique
    {
        Strict,
        Normalized
    };

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
                            new Options { Targets = new List<string>() {"[options]", "package-url..." } })
                    };
                }
            }

            [Value(0, Required = true, HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }

            [Option('a', "all-strategies", Required = false, Default = false,
                HelpText = "Execute all strategies, even after a successful one is identified.")]
            public bool AllStrategies { get; set; }

            [Option("specific-strategies", Required = false,
                HelpText = "Execute specific strategies, comma-separated.")]
            public string? SpecificStrategies { get; set; }

            [Option('s', "source-ref", Required = false, Default = "",
                HelpText = "If a source version cannot be identified, use the specified git reference (tag, commit, etc.).")]
            public string OverrideSourceReference { get; set; } = "";

            [Option("diff-technique", Required = false, Default = DiffTechnique.Normalized, HelpText = "Configure diff technique.")]
            public DiffTechnique DiffTechnique { get; set; } = DiffTechnique.Normalized;

            [Option('o', "output-file", Required = false, Default = "", HelpText = "Send the command output to a file instead of standard output")]
            public string OutputFile { get; set; } = "";

            [Option('d', "show-differences", Required = false, Default = false,
                HelpText = "Output the differences between the package and the reference content.")]
            public bool ShowDifferences { get; set; }

            [Option("show-all-differences", Required = false, Default = false,
                HelpText = "Show all differences (default: capped at 20), implies --show-differences")]
            public bool ShowAllDifferences { get; set; }

            [Option('l', "leave-intermediate", Required = false, Default = false,
                HelpText = "Do not clean up intermediate files (useful for debugging).")]
            public bool LeaveIntermediateFiles { get; set; }
        }

        public ReproducibleTool() : base()
        {
        }

        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        public static async Task Main(string[] args)
        {
            ShowToolBanner();
            Console.WriteLine();
            var reproducibleTool = new ReproducibleTool();
            await reproducibleTool.ParseOptions<Options>(args).WithParsedAsync(reproducibleTool.RunAsync);
        }

        /// <summary>
        /// Algorithm: 0.0 = Worst, 1.0 = Best 1.0 =&gt; Bit for bit archive match. @TODO Refactor
        /// this into the individual strategy objects.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public KeyValuePair<double, string> GetReproducibilityScore(ReproducibleToolResult fullResult)
        {
            if (fullResult.Results == null)
            {
                return KeyValuePair.Create(0.0, "No reproducibility results were created.");
            }

            var bestScore = KeyValuePair.Create(0.0, "No strategies were able to successfully derive the package from the source code.");

            foreach (var result in fullResult.Results)
            {
                var filesString = result.NumIgnoredFiles == 1 ? "file" : "files";

                if (string.Equals(result.StrategyName, "PackageMatchesSourceStrategy"))
                {
                    if (result.IsSuccess && !result.IsError)
                    {
                        if (result.NumIgnoredFiles == 0)
                        {
                            if (bestScore.Key < 0.80)
                            {
                                bestScore = KeyValuePair.Create(0.80, $"Package contents match the source repository contents, file-by-file, with no ignored {filesString}.");
                            }
                        }
                        else
                        {
                            if (bestScore.Key < 0.70)
                            {
                                bestScore = KeyValuePair.Create(0.70, $"Package contents match the source repository contents, file-by-file, with {result.NumIgnoredFiles} ignored {filesString}.");
                            }
                        }
                    }
                }
                else if (string.Equals(result.StrategyName, "PackageContainedInSourceStrategy"))
                {
                    if (result.IsSuccess && !result.IsError)
                    {
                        if (result.NumIgnoredFiles == 0)
                        {
                            if (bestScore.Key < 0.75)
                            {
                                bestScore = KeyValuePair.Create(0.75, $"Package is a subset of the source repository contents, with no ignored {filesString}.");
                            }
                        }
                        else
                        {
                            if (bestScore.Key < 0.65)
                            {
                                bestScore = KeyValuePair.Create(0.65, $"Package is a subset of the source repository contents, with {result.NumIgnoredFiles} ignored {filesString}.");
                            }
                        }
                    }
                }
                else if (string.Equals(result.StrategyName, "AutoBuildProducesSamePackage"))
                {
                    if (result.IsSuccess && !result.IsError)
                    {
                        if (result.NumIgnoredFiles == 0)
                        {
                            if (bestScore.Key < 0.90)
                            {
                                bestScore = KeyValuePair.Create(0.90, "Package was re-built from source, with no ignored {filesString}.");
                            }
                        }
                        else
                        {
                            if (bestScore.Key < 0.65)
                            {
                                bestScore = KeyValuePair.Create(0.65, $"Package was re-built from source, with {result.NumIgnoredFiles} ignored {filesString}.");
                            }
                        }
                    }
                }
                else if (string.Equals(result.StrategyName, "OryxBuildStrategy"))
                {
                    if (result.IsSuccess && !result.IsError)
                    {
                        if (result.NumIgnoredFiles == 0)
                        {
                            if (bestScore.Key < 0.90)
                            {
                                bestScore = KeyValuePair.Create(0.90, "Package was re-built from source, with no ignored {filesString}.");
                            }
                        }
                        else
                        {
                            if (bestScore.Key < 0.65)
                            {
                                bestScore = KeyValuePair.Create(0.65, $"Package was re-built from source, with {result.NumIgnoredFiles} ignored {filesString}.");
                            }
                        }
                    }
                }
            }
            return bestScore;
        }

        private async Task RunAsync(Options options)
        {
            if (options.ShowAllDifferences)
            {
                options.ShowDifferences = true;
            }

            // Validate strategies (before we do any other processing
            IEnumerable<Type>? runSpecificStrategies = null;
            if (options.SpecificStrategies != null)
            {
                var requestedStrategies = options.SpecificStrategies.Split(',').Select(s => s.Trim().ToLowerInvariant()).Distinct();
                runSpecificStrategies = typeof(BaseStrategy).Assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(BaseStrategy)))
                    .Where(t => requestedStrategies.Contains(t.Name.ToLowerInvariant()));
                Logger.Debug("Specific strategies requested: {0}", string.Join(", ", runSpecificStrategies.Select(t => t.Name)));

                if (requestedStrategies.Count() != runSpecificStrategies.Count())
                {
                    Logger.Debug("Invalid strategies.");
                    Console.WriteLine("Invalid strategy, available options are:");
                    var allStrategies = typeof(BaseStrategy).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseStrategy)));
                    foreach (var s in allStrategies)
                    {
                        Console.WriteLine($" * {s.Name}");
                    }
                    Console.WriteLine("Example: oss-reproducible --specific-strategies AutoBuildProducesSamePackage,PackageMatchesSourceStrategy pkg:npm/left-pad@1.3.0");
                    return;
                }
            }

            // Expand targets
            var targets = new List<string>();
            foreach (var target in options.Targets ?? Array.Empty<string>())
            {
                var purl = new PackageURL(target);
                var downloader = new PackageDownloader(purl, "temp");
                foreach (var version in downloader.PackageVersions)
                {
                    targets.Add(version.ToString());
                }
            }
            var finalResults = new List<ReproducibleToolResult>();

            foreach (var target in targets)
            {
                try
                {
                    Console.WriteLine($"Analyzing {target}...");
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
                    Console.WriteLine("Downloading package...");
                    var packageDownloader = new PackageDownloader(purl, Path.Join(tempDirectoryName, "package"));
                    var downloadResults = await packageDownloader.DownloadPackageLocalCopy(purl, false, true);

                    if (!downloadResults.Any())
                    {
                        Logger.Debug("Unable to download package.");
                    }

                    // Locate the source
                    Console.WriteLine("Locating source...");
                    var findSourceTool = new FindSourceTool();
                    var sourceMap = await findSourceTool.FindSourceAsync(purl);
                    if (sourceMap.Any())
                    {
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
                            Logger.Debug("Unable to download source.");
                        }
                    }
                    else
                    {
                        Logger.Debug("Unable to locate source repository.");
                    }

                    // Execute all available strategies
                    var strategyOptions = new StrategyOptions()
                    {
                        PackageDirectory = Path.Join(tempDirectoryName, "package"),
                        SourceDirectory = Path.Join(tempDirectoryName, "src"),
                        PackageUrl = purl,
                        TemporaryDirectory = Path.GetFullPath(tempDirectoryName),
                        DiffTechnique = options.DiffTechnique
                    };

                    // First, check to see how many strategies apply
                    var strategies = runSpecificStrategies;
                    if (strategies == null || !strategies.Any())
                    {
                        strategies = BaseStrategy.GetStrategies(strategyOptions) ?? Array.Empty<Type>();
                    }

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

                    Console.Write($"Out of {Yellow(strategies.Count().ToString())} potential strategies, {Yellow(numStrategiesApplies.ToString())} apply. ");
                    if (options.AllStrategies)
                    {
                        Console.WriteLine("Analysis will continue even after a successful strategy is found.");
                    }
                    else
                    {
                        Console.WriteLine("Analysis will stop after the first successful strategy is found.");
                    }
                    List<StrategyResult> strategyResults = new List<StrategyResult>();

                    Console.WriteLine($"\n{Blue("Results: ")}");

                    var hasSuccessfulStrategy = false;

                    foreach (var strategy in strategies)
                    {
                        var ctor = strategy.GetConstructor(new Type[] { typeof(StrategyOptions) });
                        if (ctor != null)
                        {
                            // Create a temporary directory, copy the contents from source/package
                            // so that this strategy can modify the contents without affecting other strategies.
                            var tempStrategyOptions = new StrategyOptions
                            {
                                PackageDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name, "package"),
                                SourceDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name, "src"),
                                TemporaryDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name),
                                PackageUrl = strategyOptions.PackageUrl,
                                IncludeDiffoscope = options.ShowDifferences
                            };

                            try
                            {
                                Helpers.DirectoryCopy(strategyOptions.PackageDirectory, tempStrategyOptions.PackageDirectory);
                                Helpers.DirectoryCopy(strategyOptions.SourceDirectory, tempStrategyOptions.SourceDirectory);
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug(ex, "Error copying directory for strategy. Aborting execution.");
                            }

                            Directory.CreateDirectory(tempStrategyOptions.PackageDirectory);
                            Directory.CreateDirectory(tempStrategyOptions.SourceDirectory);

                            try
                            {
                                var strategyObject = (BaseStrategy)(ctor.Invoke(new object?[] { tempStrategyOptions }));
                                StrategyResult? strategyResult = strategyObject.Execute();

                                if (strategyResult != null)
                                {
                                    strategyResults.Add(strategyResult);
                                }

                                if (strategyResult != null)
                                {
                                    if (strategyResult.IsSuccess)
                                    {
                                        Console.WriteLine($" [{Bold().Yellow("PASS")}] {Yellow(strategy.Name)}");
                                        hasSuccessfulStrategy = true;
                                    }
                                    else
                                    {
                                        Console.WriteLine($" [{Red("FAIL")}] {Red(strategy.Name)}");
                                    }

                                    if (options.ShowDifferences)
                                    {
                                        foreach (var resultMessage in strategyResult.Messages)
                                        {
                                            if (resultMessage.Filename != null && resultMessage.CompareFilename != null)
                                            {
                                                Console.WriteLine($"  {Bright.Black("(")}{Blue("P ")}{Bright.Black(")")} {resultMessage.Filename}");
                                                Console.WriteLine($"  {Bright.Black("(")}{Blue(" S")}{Bright.Black(")")} {resultMessage.CompareFilename}");
                                            }
                                            else if (resultMessage.Filename != null)
                                            {
                                                Console.WriteLine($"  {Bright.Black("(")}{Blue("P+")}{Bright.Black(")")} {resultMessage.Filename}");
                                            }
                                            else if (resultMessage.CompareFilename != null)
                                            {
                                                Console.WriteLine($"  {Bright.Black("(")}{Blue("S+")}{Bright.Black(")")} {resultMessage.CompareFilename}");
                                            }

                                            var differences = resultMessage.Differences ?? Array.Empty<DiffPiece>();

                                            var maxShowDifferences = 20;
                                            var numShowDifferences = 0;

                                            foreach (var diff in differences)
                                            {
                                                if (!options.ShowAllDifferences && numShowDifferences > maxShowDifferences)
                                                {
                                                    Console.WriteLine(Background.Blue(Bold().White("NOTE: Additional differences exist but are not shown. Pass --show-all-differences to view them all.")));
                                                    break;
                                                }

                                                switch (diff.Type)
                                                {
                                                    case ChangeType.Inserted:
                                                        Console.WriteLine($"{Bright.Black(diff.Position + ")")}\t{Red("+")} {Blue(diff.Text)}");
                                                        ++numShowDifferences;
                                                        break;

                                                    case ChangeType.Deleted:
                                                        Console.WriteLine($"\t{Green("-")} {Green(diff.Text)}");
                                                        ++numShowDifferences;
                                                        break;

                                                    default:
                                                        break;
                                                }
                                            }
                                            if (numShowDifferences > 0)
                                            {
                                                Console.WriteLine();
                                            }
                                        }

                                        var diffoscopeFile = Guid.NewGuid().ToString() + ".html";
                                        File.WriteAllText(diffoscopeFile, strategyResult.Diffoscope);
                                        Console.WriteLine($"  Diffoscope results written to {diffoscopeFile}.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine(Green($"  [-] {strategy.Name}"));
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, "Error processing {0}: {1}", strategy, ex.Message);
                                Logger.Debug(ex.StackTrace);
                            }
                        }

                        if (hasSuccessfulStrategy && !options.AllStrategies)
                        {
                            break;  // We don't need to continue
                        }
                    }

                    var reproducibilityToolResult = new ReproducibleToolResult
                    {
                        PackageUrl = purl.ToString(),
                        Results = strategyResults
                    };

                    finalResults.Add(reproducibilityToolResult);

                    var (score, scoreText) = GetReproducibilityScore(reproducibilityToolResult);
                    Console.WriteLine($"\n{Blue("Summary:")}");
                    var scoreDisplay = $"{(score * 100.0):0.#}";
                    if (reproducibilityToolResult.IsReproducible)
                    {
                        Console.WriteLine($"  [{Yellow(scoreDisplay + "%")}] {Yellow(scoreText)}");
                    }
                    else
                    {
                        Console.WriteLine($"  [{Red(scoreDisplay + "%")}] {Red(scoreText)}");
                    }

                    if (options.LeaveIntermediateFiles)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Intermediate files are located in [{tempDirectoryName}].");
                    }
                    else
                    {
                        Helpers.DeleteDirectory(tempDirectoryName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    Logger.Debug(ex.StackTrace);
                }
            }

            if (finalResults.Any())
            {
                // Write the output somewhere
                var jsonResults = Newtonsoft.Json.JsonConvert.SerializeObject(finalResults, Newtonsoft.Json.Formatting.Indented);
                if (!string.IsNullOrWhiteSpace(options.OutputFile) && !string.Equals(options.OutputFile, "-", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        File.WriteAllText(options.OutputFile, jsonResults);
                        Console.WriteLine($"Detailed results are available in {options.OutputFile}.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Unable to write to {0}. Writing to console instead.", options.OutputFile);
                        Console.Error.WriteLine(jsonResults);
                    }
                }
                else if (string.Equals(options.OutputFile, "-", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.Error.WriteLine(jsonResults);
                }
            }
            else
            {
                Logger.Debug("No results were produced.");
            }
        }
    }
}