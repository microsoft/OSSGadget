// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.ApplicationInspector.Commands;
using Microsoft.ApplicationInspector.RulesEngine;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.CST.RecursiveExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource.OssGadget.Tools
{
    using Microsoft.Extensions.Options;
    using OssGadget.Options;
    using PackageManagers;
    using PackageUrl;

    public class CharacteristicTool : BaseTool<CharacteristicToolOptions>
    {
        public CharacteristicTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public CharacteristicTool() : this(new ProjectManagerFactory()) 
        {
        }

        public async Task<AnalyzeResult?> AnalyzeFile(CharacteristicToolOptions options, string file, RuleSet? embeddedRules = null)
        {
            Logger.Trace("AnalyzeFile({0})", file);
            return await AnalyzeDirectory(options, file, embeddedRules);
        }

        /// <summary>
        ///     Analyzes a directory of files.
        /// </summary>
        /// <param name="directory"> directory to analyze. </param>
        /// <param name="embeddedRules"> Optional embedded rules to use instead of file-based rules. </param>
        /// <returns> List of tags identified </returns>
        public async Task<AnalyzeResult?> AnalyzeDirectory(CharacteristicToolOptions options, string directory, RuleSet? embeddedRules = null)
        {
            Logger.Trace("AnalyzeDirectory({0})", directory);

            AnalyzeResult? analysisResult = null;

            try
            {
                // Build the RuleSet
                RuleSet rules = new RuleSet();

                if (embeddedRules != null && embeddedRules.Any())
                {
                    // Use the embedded rules directly
                    rules.AddRange(embeddedRules);
                    Logger.Debug("Using {0} embedded rules", rules.Count());
                }
                else
                {
                    // Load from custom directory or use defaults
                    if (!options.DisableDefaultRules)
                    {
                        // Load default ApplicationInspector rules
                        var aiAssembly = typeof(AnalyzeCommand).Assembly;
                        foreach (string? resourceName in aiAssembly.GetManifestResourceNames())
                        {
                            if (resourceName.EndsWith(".json"))
                            {
                                try
                                {
                                    using var stream = aiAssembly.GetManifestResourceStream(resourceName);
                                    using var reader = new StreamReader(stream ?? new MemoryStream());
                                    rules.AddString(reader.ReadToEnd(), resourceName);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warn(ex, "Error loading default rule {0}: {1}", resourceName, ex.Message);
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(options.CustomRuleDirectory))
                    {
                        rules.AddDirectory(options.CustomRuleDirectory);
                    }
                }

                if (!rules.Any())
                {
                    Logger.Error("No rules were loaded, unable to continue.");
                    return null;
                }

                Logger.Debug("Loaded {0} total rules for analysis", rules.Count());

                // Create RuleProcessor with empty options (like DetectCryptographyTool does)
                RuleProcessor processor = new RuleProcessor(rules, new RuleProcessorOptions());

                // Get list of files to analyze
                string[] fileList;
                string[] exclusions = options.FilePathExclusions?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

                if (System.IO.Directory.Exists(directory))
                {
                    fileList = System.IO.Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                }
                else if (File.Exists(directory))
                {
                    fileList = new string[] { directory };
                }
                else
                {
                    Logger.Warn("{0} is neither a directory nor a file.", directory);
                    return null;
                }

                // Filter out excluded files
                if (exclusions.Any())
                {
                    fileList = fileList.Where(f => !exclusions.Any(exc => f.Contains(exc, StringComparison.OrdinalIgnoreCase))).ToArray();
                }

                Logger.Debug("Analyzing {0} files in {1}", fileList.Length, directory);

                // Analyze files
                List<MatchRecord> allMatches = new List<MatchRecord>();
                Dictionary<string, int> languageCounts = new Dictionary<string, int>();

                foreach (string filename in fileList)
                {
                    try
                    {
                        Logger.Trace("Processing {0}", filename);

                        byte[] fileContents = File.ReadAllBytes(filename);

                        // Create a FileEntry for the processor
                        FileEntry fileEntry = new FileEntry(filename, new MemoryStream(fileContents));
                        
                        // Determine language
                        LanguageInfo languageInfo = new LanguageInfo();
                        var languages = new Languages();
                        languages.FromFileName(filename, ref languageInfo);

                        // DEBUG: Log file processing details
                        Logger.Debug("File: {0}, Language: {1}, Size: {2} bytes", 
                            Path.GetFileName(filename), 
                            languageInfo.Name ?? "unknown", 
                            fileContents.Length);

                        // Track language statistics
                        if (!string.IsNullOrEmpty(languageInfo.Name))
                        {
                            if (languageCounts.ContainsKey(languageInfo.Name))
                                languageCounts[languageInfo.Name]++;
                            else
                                languageCounts[languageInfo.Name] = 1;
                        }

                        // Analyze the file
                        List<MatchRecord> fileMatches;

                        if (options.SingleThread)
                        {
                            fileMatches = processor.AnalyzeFile(fileEntry, languageInfo);
                        }
                        else
                        {
                            // Run with timeout for safety
                            var task = Task.Run(() => processor.AnalyzeFile(fileEntry, languageInfo));
                            if (task.Wait(TimeSpan.FromSeconds(30)))
                            {
                                fileMatches = task.Result;
                            }
                            else
                            {
                                Logger.Warn("Analysis timed out for {0}", filename);
                                continue;
                            }
                        }

                        // DEBUG: Log match results
                        Logger.Debug("File {0} produced {1} matches", 
                            Path.GetFileName(filename), 
                            fileMatches?.Count ?? 0);

                        if (fileMatches != null && fileMatches.Any())
                        {
                            allMatches.AddRange(fileMatches);
                            Logger.Trace("Found {0} matches in {1}", fileMatches.Count, filename);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error analyzing file {0}: {1}", filename, ex.Message);
                    }
                }

                // Build the AnalyzeResult  
                // Note: We can't use MetaData directly because many properties are read-only
                // and Languages dictionary might be null. We'll just pass the matches to AnalyzeResult
                // and let ApplicationInspector handle the metadata construction.

                // Create a simple AnalyzeResult without complex MetaData manipulation
                analysisResult = new AnalyzeResult()
                {
                    ResultCode = AnalyzeResult.ExitCode.Success
                };

                // Try to set basic metadata if the Metadata property allows it
                try
                {
                    MetaData metadata = new MetaData(directory, directory);
                    
                    // Try to set matches via reflection
                    var matchesField = typeof(MetaData).GetField("_matches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (matchesField != null)
                    {
                        matchesField.SetValue(metadata, allMatches);
                    }
                    
                    // Try to set properties via reflection on backing fields
                    var metadataType = typeof(MetaData);
                    
                    metadataType.GetField("<TotalFiles>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.SetValue(metadata, fileList.Length);
                    
                    metadataType.GetField("<FilesAnalyzed>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.SetValue(metadata, fileList.Length);
                    
                    metadataType.GetField("<TotalMatchesCount>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.SetValue(metadata, allMatches.Count);
                    
                    metadataType.GetField("<UniqueMatchesCount>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.SetValue(metadata, allMatches.Select(m => m.RuleId).Distinct().Count());
                    
                    // Try to initialize and set Languages dictionary
                    if (languageCounts.Any())
                    {
                        var languagesField = metadataType.GetField("<Languages>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (languagesField != null)
                        {
                            var existingDict = languagesField.GetValue(metadata) as System.Collections.Concurrent.ConcurrentDictionary<string, int>;
                            if (existingDict == null)
                            {
                                // Create new dictionary and set it
                                existingDict = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
                                languagesField.SetValue(metadata, existingDict);
                            }
                            
                            // Add language counts
                            foreach (var kvp in languageCounts)
                            {
                                existingDict.TryAdd(kvp.Key, kvp.Value);
                            }
                        }
                    }
                    
                    // Set the metadata on the result
                    var metadataProperty = typeof(AnalyzeResult).GetProperty("Metadata");
                    if (metadataProperty != null && metadataProperty.CanWrite)
                    {
                        metadataProperty.SetValue(analysisResult, metadata);
                    }
                }
                catch (Exception metadataEx)
                {
                    Logger.Warn(metadataEx, "Unable to set full metadata, continuing with basic result: {0}", metadataEx.Message);
                }

                Logger.Debug("Operation Complete: {0} files analyzed, {1} matches found.", fileList.Length, allMatches.Count);
            }
            catch (Exception ex)
            {
                Logger.Warn("Error analyzing {0}: {1}", directory, ex.Message);
            }

            return analysisResult;
        }

        /// <summary>
        ///     Analyzes a directory and returns raw match records (like DetectCryptographyTool).
        /// </summary>
        /// <param name="options">Analysis options</param>
        /// <param name="directory">Directory to analyze</param>
        /// <param name="embeddedRules">Optional embedded rules</param>
        /// <returns>List of MatchRecord objects found during analysis</returns>
        public async Task<List<MatchRecord>> AnalyzeDirectoryRaw(CharacteristicToolOptions options, string directory, RuleSet? embeddedRules = null)
        {
            Logger.Trace("AnalyzeDirectoryRaw({0})", directory);

            List<MatchRecord> allMatches = new List<MatchRecord>();

            try
            {
                // Build the RuleSet
                RuleSet rules = new RuleSet();

                if (embeddedRules != null && embeddedRules.Any())
                {
                    rules.AddRange(embeddedRules);
                    Logger.Debug("Using {0} embedded rules", rules.Count());
                }
                else
                {
                    // Load from custom directory or use defaults
                    if (!options.DisableDefaultRules)
                    {
                        var aiAssembly = typeof(AnalyzeCommand).Assembly;
                        foreach (string? resourceName in aiAssembly.GetManifestResourceNames())
                        {
                            if (resourceName.EndsWith(".json"))
                            {
                                try
                                {
                                    using var stream = aiAssembly.GetManifestResourceStream(resourceName);
                                    using var reader = new StreamReader(stream ?? new MemoryStream());
                                    rules.AddString(reader.ReadToEnd(), resourceName);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warn(ex, "Error loading default rule {0}: {1}", resourceName, ex.Message);
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(options.CustomRuleDirectory))
                    {
                        rules.AddDirectory(options.CustomRuleDirectory);
                    }
                }

                if (!rules.Any())
                {
                    Logger.Error("No rules were loaded, unable to continue.");
                    return allMatches;
                }

                Logger.Debug("Loaded {0} total rules for analysis", rules.Count());

                // Create RuleProcessor
                RuleProcessor processor = new RuleProcessor(rules, new RuleProcessorOptions());

                // Get list of files to analyze
                string[] fileList;
                string[] exclusions = options.FilePathExclusions?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

                if (System.IO.Directory.Exists(directory))
                {
                    fileList = System.IO.Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                }
                else if (File.Exists(directory))
                {
                    fileList = new string[] { directory };
                }
                else
                {
                    Logger.Warn("{0} is neither a directory nor a file.", directory);
                    return allMatches;
                }

                // Filter out excluded files
                if (exclusions.Any())
                {
                    fileList = fileList.Where(f => !exclusions.Any(exc => f.Contains(exc, StringComparison.OrdinalIgnoreCase))).ToArray();
                }

                Logger.Debug("Analyzing {0} files in {1}", fileList.Length, directory);

                // Analyze files
                foreach (string filename in fileList)
                {
                    try
                    {
                        Logger.Trace("Processing {0}", filename);

                        byte[] fileContents = File.ReadAllBytes(filename);
                        FileEntry fileEntry = new FileEntry(filename, new MemoryStream(fileContents));
                        
                        // Determine language
                        LanguageInfo languageInfo = new LanguageInfo();
                        var languages = new Languages();
                        languages.FromFileName(filename, ref languageInfo);

                        // Analyze the file
                        List<MatchRecord> fileMatches;
                
                        if (options.SingleThread)
                        {
                            fileMatches = processor.AnalyzeFile(fileEntry, languageInfo);
                        }
                        else
                        {
                            var task = Task.Run(() => processor.AnalyzeFile(fileEntry, languageInfo));
                            if (task.Wait(TimeSpan.FromSeconds(30)))
                            {
                                fileMatches = task.Result;
                            }
                            else
                            {
                                Logger.Warn("Analysis timed out for {0}", filename);
                                continue;
                            }
                        }

                        if (fileMatches != null && fileMatches.Any())
                        {
                            allMatches.AddRange(fileMatches);
                            Logger.Debug("Found {0} matches in {1}", fileMatches.Count, filename);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error analyzing file {0}: {1}", filename, ex.Message);
                    }
                }

                Logger.Debug("Operation Complete: {0} files analyzed, {1} matches found.", fileList.Length, allMatches.Count);
            }
            catch (Exception ex)
            {
                Logger.Warn("Error analyzing {0}: {1}", directory, ex.Message);
            }

            return allMatches;
        }

        /// <summary>
        ///     Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl"> The package-url of the package to analyze. </param>
        /// <returns> List of tags identified </returns>
        public async Task<Dictionary<string, AnalyzeResult?>> AnalyzePackage(CharacteristicToolOptions options, PackageURL purl,
            string? targetDirectoryName,
            bool doCaching = false,
            RuleSet? embeddedRules = null)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());

            Dictionary<string, AnalyzeResult?>? analysisResults = new Dictionary<string, AnalyzeResult?>();

            PackageDownloader? packageDownloader = new PackageDownloader(purl, ProjectManagerFactory, targetDirectoryName, doCaching);
            // ensure that the cache directory has the required package, download it otherwise
            List<string>? directoryNames = await packageDownloader.DownloadPackageLocalCopy(purl,
                false,
                true);
            if (directoryNames.Count > 0)
            {
                foreach (string? directoryName in directoryNames)
                {
                    AnalyzeResult? singleResult = await AnalyzeDirectory(options, directoryName, embeddedRules);
                    analysisResults[directoryName] = singleResult;
                }
            }
            else
            {
                Logger.Warn("Error downloading {0}.", purl.ToString());
            }
            packageDownloader.ClearPackageLocalCopyIfNoCaching();
            return analysisResults;
        }

        /// <summary>
        ///     Build and return a list of Sarif Result list from the find characterstics results
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<SarifResult> GetSarifResults(PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResult, CharacteristicToolOptions opts)
        {
            List<SarifResult> sarifResults = new List<SarifResult>();
            
            if (analysisResult.HasAtLeastOneNonNullValue())
            {
                foreach (string? key in analysisResult.Keys)
                {
                    MetaData? metadata = analysisResult?[key]?.Metadata;

                    foreach (MatchRecord? result in metadata?.Matches ?? new List<MatchRecord>())
                    {
                        SarifResult? individualResult = new SarifResult()
                        {
                            Message = new Message()
                            {
                                Text = result.RuleDescription,
                                Id = result.RuleId
                            },
                            Kind = ResultKind.Informational,
                            Level = opts.SarifLevel,
                            Locations = SarifOutputBuilder.BuildPurlLocation(purl),
                            Rule = new ReportingDescriptorReference() { Id = result.RuleId },
                        };

                        individualResult.SetProperty("Severity", result.Severity);
                        individualResult.SetProperty("Confidence", result.Confidence);

                        individualResult.Locations.Add(new CodeAnalysis.Sarif.Location()
                        {
                            PhysicalLocation = new PhysicalLocation()
                            {
                                Address = new Address() { FullyQualifiedName = result.FileName },
                                Region = new Region()
                                {
                                    StartLine = result.StartLocationLine,
                                    EndLine = result.EndLocationLine,
                                    StartColumn = result.StartLocationColumn,
                                    EndColumn = result.EndLocationColumn,
                                    SourceLanguage = result.Language,
                                    Snippet = new ArtifactContent()
                                    {
                                        Text = result.Excerpt,
                                        Rendered = new MultiformatMessageString(result.Excerpt, $"`{result.Excerpt}`", null)
                                    }
                                }
                            }
                        });
                        
                        sarifResults.Add(individualResult);
                    }
                }
            }
            return sarifResults;
        }

        /// <summary>
        ///     Convert charactersticTool results into text format
        /// </summary>
        /// <param name="results"> </param>
        /// <returns> </returns>
        private static List<string> GetTextResults(PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResult)
        {
            List<string> stringOutput = new List<string>();

            stringOutput.Add(purl.ToString());

            if (analysisResult.HasAtLeastOneNonNullValue())
            {
                foreach (string? key in analysisResult.Keys)
                {
                    MetaData? metadata = analysisResult?[key]?.Metadata;

                    stringOutput.Add(string.Format("Programming Language(s): {0}",
                        string.Join(", ", metadata?.Languages?.Keys ?? new List<string>())));
                    
                    stringOutput.Add("Unique Tags (Confidence): ");
                    bool hasTags = false;
                    Dictionary<string, List<Confidence>>? dict = new Dictionary<string, List<Confidence>>();
                    foreach ((string[]? tags, Confidence confidence) in metadata?.Matches?.Where(x => x is not null).Select(x => (x.Tags ?? Array.Empty<string>(), x.Confidence)) ?? Array.Empty<(string[], Confidence)>())
                    {
                        foreach (string? tag in tags)
                        {
                            if (dict.ContainsKey(tag))
                            {
                                dict[tag].Add(confidence);
                            }
                            else
                            {
                                dict[tag] = new List<Confidence>() { confidence };
                            }
                        }
                    }

                    foreach ((string? k, List<Confidence>? v) in dict)
                    {
                        hasTags = true;
                        Confidence confidence = v.Max();
                        if (confidence > 0)
                        {
                            stringOutput.Add($" * {k} ({v.Max()})");
                        }
                        else
                        {
                            stringOutput.Add($" * {k}");
                        }
                    }
                    if (!hasTags)
                    {
                        stringOutput.Add("No tags were found.");
                    }
                }
            }
            return stringOutput;
        }

        /// <summary>
        ///     Convert charactersticTool results into output format
        /// </summary>
        /// <param name="outputBuilder"> </param>
        /// <param name="purl"> </param>
        /// <param name="results"> </param>
        private void AppendOutput(IOutputBuilder outputBuilder, PackageURL purl, Dictionary<string, AnalyzeResult?> analysisResults, CharacteristicToolOptions opts)
        {
            switch (currentOutputFormat)
            {
                case OutputFormat.text:
                default:
                    outputBuilder.AppendOutput(GetTextResults(purl, analysisResults));
                    break;

                case OutputFormat.sarifv1:
                case OutputFormat.sarifv2:
                    outputBuilder.AppendOutput(GetSarifResults(purl, analysisResults,opts));
                    break;
            }
        }

        public override async Task<ErrorCode> RunAsync(CharacteristicToolOptions options)
        {
            _ = await LegacyRunAsync(options);
            return ErrorCode.Ok;
        }
        
        public async Task<List<Dictionary<string, AnalyzeResult?>>> LegacyRunAsync(CharacteristicToolOptions options, RuleSet? embeddedRules = null)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(options.Format);

            List<Dictionary<string, AnalyzeResult?>>? finalResults = new List<Dictionary<string, AnalyzeResult?>>();

            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        if (target.StartsWith("pkg:"))
                        {
                            PackageURL? purl = new PackageURL(target);
                            string downloadDirectory = options.DownloadDirectory == "." ? System.IO.Directory.GetCurrentDirectory() : options.DownloadDirectory;
                            Dictionary<string, AnalyzeResult?>? analysisResult = await AnalyzePackage(options, purl,
                                downloadDirectory,
                                options.UseCache == true,
                                embeddedRules);

                            AppendOutput(outputBuilder, purl, analysisResult, options);
                            finalResults.Add(analysisResult);
                        }
                        else if (System.IO.Directory.Exists(target))
                        {
                            AnalyzeResult? analysisResult = await AnalyzeDirectory(options, target, embeddedRules);
                            if (analysisResult != null)
                            {
                                Dictionary<string, AnalyzeResult?>? analysisResults = new Dictionary<string, AnalyzeResult?>()
                                {
                                    { target, analysisResult }
                                };
                                PackageURL? purl = new PackageURL("generic", target);
                                AppendOutput(outputBuilder, purl, analysisResults, options);
                            }
                            finalResults.Add(new Dictionary<string, AnalyzeResult?>() { { target, analysisResult } });

                        }
                        else if (File.Exists(target))
                        {
                            AnalyzeResult? analysisResult = await AnalyzeFile(options, target, embeddedRules);
                            if (analysisResult != null)
                            {
                                Dictionary<string, AnalyzeResult?>? analysisResults = new Dictionary<string, AnalyzeResult?>()
                                {
                                    { target, analysisResult }
                                };
                                PackageURL? purl = new PackageURL("generic", target);
                                AppendOutput(outputBuilder, purl, analysisResults, options);
                            }
                            finalResults.Add(new Dictionary<string, AnalyzeResult?>() { { target, analysisResult } });
                        }
                        else
                        {
                            Logger.Warn("Package or file identifier was invalid.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                    }
                }
                outputBuilder.PrintOutput();
            }

            RestoreOutput();
            return finalResults;
        }
    }
}
