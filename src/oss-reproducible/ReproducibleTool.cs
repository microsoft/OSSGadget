// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Reproducibility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static Crayon.Output;

namespace Microsoft.CST.OpenSource
{
    using Microsoft.CST.OpenSource.Helpers;
    using PackageManagers;
    using PackageUrl;

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
                        new Example("Estimate semantic equivalency of the given package and source code", new Options { Targets = new List<string>() {"[options]", "package-url..." } })
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

        public ReproducibleTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public ReproducibleTool() : this(new ProjectManagerFactory())
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

            ReproducibleTool? reproducibleTool = new ReproducibleTool();
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
                return KeyValuePair.Create(0.0, "No semantic equivalency results were created.");
            }

            KeyValuePair<double, string> bestScore = KeyValuePair.Create(0.0, "No strategies were able to successfully derive the package from the source code.");

            foreach (StrategyResult? result in fullResult.Results)
            {
                string? filesString = result.NumIgnoredFiles == 1 ? "file" : "files";

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
                                bestScore = KeyValuePair.Create(0.90, $"Package was re-built from source, with no ignored {filesString}.");
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
                                bestScore = KeyValuePair.Create(0.90, $"Package was re-built from source, with no ignored {filesString}.");
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
                IEnumerable<string>? requestedStrategies = options.SpecificStrategies.Split(',').Select(s => s.Trim().ToLowerInvariant()).Distinct();
                runSpecificStrategies = typeof(BaseStrategy).Assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(BaseStrategy)))
                    .Where(t => requestedStrategies.Contains(t.Name.ToLowerInvariant()));
                Logger.Debug("Specific strategies requested: {0}", string.Join(", ", runSpecificStrategies.Select(t => t.Name)));

                if (requestedStrategies.Count() != runSpecificStrategies.Count())
                {
                    Logger.Debug("Invalid strategies.");
                    Console.WriteLine("Invalid strategy, available options are:");
                    IEnumerable<Type>? allStrategies = typeof(BaseStrategy).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseStrategy)));
                    foreach (Type? s in allStrategies)
                    {
                        Console.WriteLine($" * {s.Name}");
                    }
                    Console.WriteLine("Example: oss-reproducible --specific-strategies AutoBuildProducesSamePackage,PackageMatchesSourceStrategy pkg:npm/left-pad@1.3.0");
                    return;
                }
            }

            // Expand targets
            List<string>? targets = new List<string>();
            foreach (string? target in options.Targets ?? Array.Empty<string>())
            {
                PackageURL? purl = new PackageURL(target);
                PackageDownloader? downloader = new PackageDownloader(purl, ProjectManagerFactory, "temp");
                foreach (PackageURL? version in downloader.PackageVersions)
                {
                    targets.Add(version.ToString());
                }
            }
            List<ReproducibleToolResult>? finalResults = new List<ReproducibleToolResult>();

            foreach (string? target in targets)
            {
                try
                {
                    Console.WriteLine($"Analyzing {target}...");
                    Logger.Debug("Processing: {0}", target);

                    PackageURL? purl = new PackageURL(target);
                    if (purl.Version == null)
                    {
                        Logger.Error("Package is missing a version, which is required for this tool.");
                        continue;
                    }

                    string? tempDirectoryName = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString().Substring(0, 8));
                    FileSystemHelper.RetryDeleteDirectory(tempDirectoryName);
                    // Download the package
                    Console.WriteLine("Downloading package...");
                    PackageDownloader? packageDownloader = new PackageDownloader(purl, ProjectManagerFactory, Path.Join(tempDirectoryName, "package"));
                    List<string>? downloadResults = await packageDownloader.DownloadPackageLocalCopy(purl, false, true);

                    if (!downloadResults.Any())
                    {
                        Logger.Debug("Unable to download package.");
                    }

                    // Locate the source
                    Console.WriteLine("Locating source...");
                    FindSourceTool? findSourceTool = new FindSourceTool(ProjectManagerFactory);
                    Dictionary<PackageURL, double>? sourceMap = await findSourceTool.FindSourceAsync(purl);
                    if (sourceMap.Any())
                    {
                        List<KeyValuePair<PackageURL, double>>? sourceMapList = sourceMap.ToList();
                        sourceMapList.Sort((a, b) => a.Value.CompareTo(b.Value));
                        PackageURL? bestSourcePurl = sourceMapList.Last().Key;
                        if (string.IsNullOrEmpty(bestSourcePurl.Version))
                        {
                            // Tie back the original version to the new PackageURL
                            bestSourcePurl = new PackageURL(bestSourcePurl.Type, bestSourcePurl.Namespace, bestSourcePurl.Name,
                                                            purl.Version, bestSourcePurl.Qualifiers, bestSourcePurl.Subpath);
                        }
                        Logger.Debug("Identified best source code repository: {0}", bestSourcePurl);

                        // Download the source
                        Console.WriteLine("Downloading source...");
                        foreach (string? reference in new[] { bestSourcePurl.Version, options.OverrideSourceReference, "master", "main" })
                        {
                            if (string.IsNullOrWhiteSpace(reference))
                            {
                                continue;
                            }
                            Logger.Debug("Trying to download package, version/reference [{0}].", reference);
                            PackageURL? purlRef = new PackageURL(bestSourcePurl.Type, bestSourcePurl.Namespace, bestSourcePurl.Name, reference, bestSourcePurl.Qualifiers, bestSourcePurl.Subpath);
                            packageDownloader = new PackageDownloader(purlRef, ProjectManagerFactory, Path.Join(tempDirectoryName, "src"));
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
                    StrategyOptions? strategyOptions = new StrategyOptions()
                    {
                        PackageDirectory = Path.Join(tempDirectoryName, "package"),
                        SourceDirectory = Path.Join(tempDirectoryName, "src"),
                        PackageUrl = purl,
                        TemporaryDirectory = Path.GetFullPath(tempDirectoryName),
                        DiffTechnique = options.DiffTechnique
                    };

                    // First, check to see how many strategies apply
                    IEnumerable<Type>? strategies = runSpecificStrategies;
                    if (strategies == null || !strategies.Any())
                    {
                        strategies = BaseStrategy.GetStrategies(strategyOptions) ?? Array.Empty<Type>();
                    }

                    int numStrategiesApplies = 0;
                    foreach (Type? strategy in strategies)
                    {
                        ConstructorInfo? ctor = strategy.GetConstructor(new Type[] { typeof(StrategyOptions) });
                        if (ctor != null)
                        {
                            BaseStrategy? strategyObject = (BaseStrategy)(ctor.Invoke(new object?[] { strategyOptions }));
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

                    bool hasSuccessfulStrategy = false;

                    foreach (Type? strategy in strategies)
                    {
                        ConstructorInfo? ctor = strategy.GetConstructor(new Type[] { typeof(StrategyOptions) });
                        if (ctor != null)
                        {
                            // Create a temporary directory, copy the contents from source/package
                            // so that this strategy can modify the contents without affecting other strategies.
                            StrategyOptions? tempStrategyOptions = new StrategyOptions
                            {
                                PackageDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name, "package"),
                                SourceDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name, "src"),
                                TemporaryDirectory = Path.Join(strategyOptions.TemporaryDirectory, strategy.Name),
                                PackageUrl = strategyOptions.PackageUrl,
                                IncludeDiffoscope = options.ShowDifferences
                            };

                            try
                            {
                                OssReproducibleHelpers.DirectoryCopy(strategyOptions.PackageDirectory, tempStrategyOptions.PackageDirectory);
                                OssReproducibleHelpers.DirectoryCopy(strategyOptions.SourceDirectory, tempStrategyOptions.SourceDirectory);
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug(ex, "Error copying directory for strategy. Aborting execution.");
                            }

                            System.IO.Directory.CreateDirectory(tempStrategyOptions.PackageDirectory);
                            System.IO.Directory.CreateDirectory(tempStrategyOptions.SourceDirectory);

                            try
                            {
                                BaseStrategy? strategyObject = (BaseStrategy)(ctor.Invoke(new object?[] { tempStrategyOptions }));
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
                                        foreach (StrategyResultMessage? resultMessage in strategyResult.Messages)
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

                                            IEnumerable<DiffPiece>? differences = resultMessage.Differences ?? Array.Empty<DiffPiece>();

                                            int maxShowDifferences = 20;
                                            int numShowDifferences = 0;

                                            foreach (DiffPiece? diff in differences)
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

                                        string? diffoscopeFile = Guid.NewGuid().ToString() + ".html";
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

                    ReproducibleToolResult? reproducibilityToolResult = new ReproducibleToolResult
                    {
                        PackageUrl = purl.ToString(),
                        Results = strategyResults
                    };

                    finalResults.Add(reproducibilityToolResult);

                    (double score, string scoreText) = GetReproducibilityScore(reproducibilityToolResult);
                    Console.WriteLine($"\n{Blue("Summary:")}");
                    string? scoreDisplay = $"{(score * 100.0):0.#}";
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
                        FileSystemHelper.RetryDeleteDirectory(tempDirectoryName);
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
                string? jsonResults = Newtonsoft.Json.JsonConvert.SerializeObject(finalResults, Newtonsoft.Json.Formatting.Indented);
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
                        Console.WriteLine(jsonResults);
                    }
                }
                else if (string.Equals(options.OutputFile, "-", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine(jsonResults);
                }
            }
            else
            {
                Logger.Debug("No results were produced.");
            }
        }
    }
}
