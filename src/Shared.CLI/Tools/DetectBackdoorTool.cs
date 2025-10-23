// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.ApplicationInspector.RulesEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Crayon.Output;

namespace Microsoft.CST.OpenSource
{
    using OssGadget.Options;
    using OssGadget.Tools;
    using PackageManagers;
    using PackageUrl;

    public class DetectBackdoorTool : BaseTool<DetectBackdoorToolOptions>
    {
        public DetectBackdoorTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public DetectBackdoorTool() : this(new ProjectManagerFactory())
        {
        }

        public override async Task<ErrorCode> RunAsync(DetectBackdoorToolOptions options)
        {
            if (options?.Targets is not IList<string> targetList || targetList.Count == 0)
            {
                Logger.Warn("No target provided; nothing to analyze.");
                return ErrorCode.NoTargets;
            }

            // Load embedded backdoor detection rules
            RuleSet? embeddedRules = LoadEmbeddedRules();
            
            if (embeddedRules == null || !embeddedRules.Any())
            {
                Logger.Error("Failed to load embedded backdoor detection rules. Cannot proceed.");
                return ErrorCode.ProcessingException;
            }

            CharacteristicTool characteristicTool = new CharacteristicTool(ProjectManagerFactory);
            
            foreach (string target in targetList)
            {
                try
                {
                    List<MatchRecord> results = new List<MatchRecord>();

                    if (target.StartsWith("pkg:", StringComparison.InvariantCulture))
                    {
                        PackageURL purl = new PackageURL(target);
                        results = await AnalyzePackage(characteristicTool, options, purl, embeddedRules);
                    }
                    else if (System.IO.Directory.Exists(target))
                    {
                        results = await AnalyzeDirectory(characteristicTool, options, target, embeddedRules);
                    }
                    else if (File.Exists(target))
                    {
                        results = await AnalyzeDirectory(characteristicTool, options, target, embeddedRules);
                    }
                    else
                    {
                        Logger.Warn("{0} was neither a Package URL, directory, nor a file.", target);
                        continue;
                    }

                    // Display results
                    if (results == null || !results.Any())
                    {
                        Console.WriteLine("0 matches found.");
                    }
                    else
                    {
                        DisplayResults(target, results, options);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                }
            }

            return ErrorCode.Ok;
        }

        private async Task<List<MatchRecord>> AnalyzePackage(CharacteristicTool tool, DetectBackdoorToolOptions options, PackageURL purl, RuleSet embeddedRules)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());

            List<MatchRecord> allMatches = new List<MatchRecord>();
            
            PackageDownloader packageDownloader = new PackageDownloader(purl, ProjectManagerFactory, options.DownloadDirectory, options.UseCache);
            List<string> directoryNames = await packageDownloader.DownloadPackageLocalCopy(purl, false, true);
            
            if (directoryNames.Count > 0)
            {
                foreach (string directoryName in directoryNames)
                {
                    Logger.Trace("Analyzing directory {0}", directoryName);
                    List<MatchRecord> singleResult = await AnalyzeDirectory(tool, options, directoryName, embeddedRules);
                    if (singleResult != null)
                    {
                        allMatches.AddRange(singleResult);
                    }
                }
            }
            else
            {
                Logger.Warn("Error downloading {0}.", purl.ToString());
            }
            
            packageDownloader.ClearPackageLocalCopyIfNoCaching();
            return allMatches;
        }

        private async Task<List<MatchRecord>> AnalyzeDirectory(CharacteristicTool tool, DetectBackdoorToolOptions options, string directory, RuleSet embeddedRules)
        {
            CharacteristicToolOptions cOptions = new CharacteristicToolOptions
            {
                Targets = new List<string> { directory },
                DisableDefaultRules = true,
                CustomRuleDirectory = null,
                DownloadDirectory = options.DownloadDirectory,
                UseCache = options.UseCache,
                AllowTagsInBuildFiles = true,
                FilePathExclusions = ".md,LICENSE,.txt",
                AllowDupTags = true,
                EnableBacktracking = options.EnableBacktracking,
                SingleThread = options.SingleThread
            };

            return await tool.AnalyzeDirectoryRaw(cOptions, directory, embeddedRules);
        }

        private void DisplayResults(string target, List<MatchRecord> results, DetectBackdoorToolOptions options)
        {
            Console.WriteLine($"\n{target}");
            Console.WriteLine($"{results.Count} matches found.\n");

            if (options.Format == "text" && results.Any())
            {
                var orderedResults = results.OrderByDescending(x => x.Confidence);
                int matchIndex = 1;

                foreach (MatchRecord match in orderedResults)
                {
                    WriteMatch(match, matchIndex, results.Count, target);
                    matchIndex++;
                }
            }
        }

        private void WriteMatch(MatchRecord match, int index, int matchCount, string basePath)
        {
            string? filename = match.FileName;
            if (filename == null)
            {
                return;
            }

            // Trim base path if present
            if (filename.StartsWith(basePath))
            {
                filename = filename.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            Console.WriteLine(Red($"--[ ") + Blue("Match #") + Yellow(index.ToString()) + Blue(" of ") + Yellow(matchCount.ToString()) + Red(" ]--"));
            Console.WriteLine("   Rule Id: " + Blue(match.Rule.Id));
            Console.WriteLine("       Tag: " + Blue(match.Tags?.FirstOrDefault() ?? "N/A"));
            Console.WriteLine("  Severity: " + Cyan(match.Severity.ToString()) + ", Confidence: " + Cyan(match.Confidence.ToString()));
            Console.WriteLine("  Filename: " + Yellow(filename));
            Console.WriteLine("   Pattern: " + Green(match.MatchingPattern.Pattern));

            // Display excerpt
            string[] excerptLines = match.Excerpt.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int lineNumber = match.StartLocationLine;

            foreach (string line in excerptLines.Take(excerptLines.Length - 1))
            {
                string displayLine = line.Length > 100 ? line.Substring(0, 100) + "..." : line;
                Console.WriteLine(Bright.Black($"{lineNumber++} | ") + Magenta(displayLine));
            }
            Console.WriteLine();
        }

        private RuleSet? LoadEmbeddedRules()
        {
            try
            {
                var assembly = typeof(DetectBackdoorTool).Assembly;
                var resourceNames = assembly.GetManifestResourceNames()
                    .Where(name => name.Contains("BackdoorRules") && name.EndsWith(".json"))
                    .ToList();

                if (!resourceNames.Any())
                {
                    Logger.Warn("No embedded BackdoorRules found in assembly resources");
                    return null;
                }

                RuleSet rules = new RuleSet();

                foreach (var resourceName in resourceNames)
                {
                    try
                    {
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream == null)
                        {
                            Logger.Warn("Could not load resource: {0}", resourceName);
                            continue;
                        }

                        using var reader = new StreamReader(stream);
                        var jsonContent = reader.ReadToEnd();
                        rules.AddString(jsonContent, resourceName);
                        Logger.Debug("Loaded rules from {0}", resourceName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to load embedded rule from {0}: {1}", resourceName, ex.Message);
                    }
                }

                if (rules.Any())
                {
                    Logger.Info("Successfully loaded {0} total backdoor detection rules from embedded resources", rules.Count());
                    return rules;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading embedded rules: {0}", ex.Message);
                return null;
            }
        }
    }
}
