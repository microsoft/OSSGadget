// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.CST.OpenSource.Shared;
using PeNet;
using SharpDisasm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebAssembly; // Acquire from https://www.nuget.org/packages/WebAssembly
using WebAssembly.Instructions;
using Microsoft.ApplicationInspector.Commands;
using static Crayon.Output;
using Microsoft.ApplicationInspector.RulesEngine;
using Microsoft.CST.RecursiveExtractor;

namespace Microsoft.CST.OpenSource
{
    using Helpers;
    using Microsoft.CST.OpenSource.PackageManagers;
    using PackageUrl;

    public class DetectCryptographyTool : OSSGadget
    {
        /// <summary>
        /// Command line options
        /// </summary>
        public Dictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "disable-default-rules", false },
            { "custom-rule-directory", null },
            { "download-directory", null },
            { "use-cache", false },
            { "format", "text" },
            { "output-file", null },
            { "verbose", false }
        };

        private static readonly IEnumerable<string> IGNORE_FILES = new List<string>()
        {
            ".signature.p7s"
        };

        /// <summary>
        /// Main entrypoint for the download program.
        /// </summary>
        /// <param name="args">parameters passed in from the user</param>
        private static async Task Main(string[] args)
        {
            ShowToolBanner();

            DetectCryptographyTool detectCryptographyTool = new DetectCryptographyTool();

            detectCryptographyTool.ParseOptions(args);

            // select output destination and format
            detectCryptographyTool.SelectOutput((string?)detectCryptographyTool.Options["output-file"] ?? "");
            IOutputBuilder outputBuilder = detectCryptographyTool.SelectFormat((string?)detectCryptographyTool.Options["format"] ?? "text");
            if (detectCryptographyTool.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                StringBuilder? sb = new StringBuilder();
                foreach (string? target in targetList)
                {
                    sb.Clear();
                    try
                    {
                        sb.AppendLine($"{target}");

                        List<IssueRecord>? results = null;
                        if (target.StartsWith("pkg:", StringComparison.InvariantCulture))
                        {
                            PackageURL? purl = new PackageURL(target);
                            results = await (detectCryptographyTool.AnalyzePackage(purl,
                                (string?)detectCryptographyTool.Options["download-directory"] ?? string.Empty,
                                (bool?)detectCryptographyTool.Options["use-cache"] == true) ??
                                Task.FromResult(new List<IssueRecord>()));
                        }
                        else if (System.IO.Directory.Exists(target))
                        {
                            results = await (detectCryptographyTool.AnalyzeDirectory(target) ??
                                                                Task.FromResult(new List<IssueRecord>()));
                        }
                        else if (File.Exists(target))
                        {
                            string? targetDirectoryName = null;
                            while (targetDirectoryName == null || System.IO.Directory.Exists(targetDirectoryName))
                            {
                                targetDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                            }

                            string? path = await ArchiveHelper.ExtractArchiveAsync(targetDirectoryName, Path.GetFileName(target), File.OpenRead(target));

                            results = await detectCryptographyTool.AnalyzeDirectory(path);

                            // Clean up after ourselves

                        }
                        else
                        {
                            Logger.Warn($"{target} was neither a Package URL, directory, nor a file.");
                            continue;
                        }

                        if (results == null)
                        {
                            Logger.Warn("No results were generated.");
                            continue;
                        }
                        else
                        {
                            sb.AppendLine("Summary Results:");
                            sb.AppendLine(Blue("Cryptographic Implementations:"));
                            IOrderedEnumerable<string>? implementations = results.SelectMany(r => r.Issue.Rule.Tags ?? Array.Empty<string>())
                                                         .Distinct()
                                                         .Where(t => t.StartsWith("Cryptography.Implementation."))
                                                         .Select(t => t.Replace("Cryptography.Implementation.", ""))
                                                         .OrderBy(s => s);
                            if (implementations.Any())
                            {
                                foreach (string? tag in implementations)
                                {
                                    sb.AppendLine(Bright.Blue($" * {tag}"));
                                }
                            }
                            else
                            {
                                sb.AppendLine(Bright.Black("  No implementations found."));
                            }

                            sb.AppendLine();
                            sb.AppendLine(Red("Cryptographic Library References:"));
                            IOrderedEnumerable<string>? references = results.SelectMany(r => r.Issue.Rule.Tags ?? Array.Empty<string>())
                                                    .Distinct()
                                                    .Where(t => t.StartsWith("Cryptography.Reference."))
                                                    .Select(t => t.Replace("Cryptography.Reference.", ""))
                                                    .OrderBy(s => s);

                            if (references.Any())
                            {
                                foreach (string? tag in references)
                                {
                                    sb.AppendLine(Bright.Red($" * {tag}"));
                                }
                            }
                            else
                            {
                                sb.AppendLine(Bright.Black("  No library references found."));
                            }

                            sb.AppendLine();
                            sb.AppendLine(Green("Other Cryptographic Characteristics:"));
                            IOrderedEnumerable<string>? characteristics = results.SelectMany(r => r.Issue.Rule.Tags ?? Array.Empty<string>())
                                                         .Distinct()
                                                         .Where(t => t.Contains("Crypto", StringComparison.InvariantCultureIgnoreCase)&&
                                                                     !t.StartsWith("Cryptography.Implementation.") &&
                                                                     !t.StartsWith("Cryptography.Reference."))
                                                         .Select(t => t.Replace("Cryptography.", ""))
                                                         .OrderBy(s => s);
                            if (characteristics.Any())
                            {
                                foreach (string? tag in characteristics)
                                {
                                    sb.AppendLine(Bright.Green($" * {tag}"));
                                }
                            }
                            else
                            {
                                sb.AppendLine(Bright.Black("  No additional characteristics found."));
                            }

                            if ((bool?)detectCryptographyTool.Options["verbose"] == true)
                            {
                                IOrderedEnumerable<IGrouping<string, IssueRecord>>? items = results.GroupBy(k => k.Issue.Rule.Name).OrderByDescending(k => k.Count());
                                foreach (IGrouping<string, IssueRecord>? item in items)
                                {
                                    sb.AppendLine();
                                    sb.AppendLine($"There were {item.Count()} finding(s) of type [{item.Key}].");

                                    foreach (IssueRecord? result in results)
                                    {
                                        if (result.Issue.Rule.Name == item.Key)
                                        {
                                            sb.AppendLine($" {result.Filename}:");
                                            if (result.Issue.Rule.Id == "_CRYPTO_DENSITY")
                                            {
                                                // No excerpt for cryptographic density
                                                // TODO: We stuffed the density in the unused 'Description' field. This is code smell.
                                                sb.AppendLine($"  | The maximum cryptographic density is {result.Issue.Rule.Description}.");
                                            }
                                            else
                                            {
                                                // Show the excerpt
                                                foreach (string? line in result.TextSample.Split(new char[] { '\n', '\r' }))
                                                {
                                                    if (!string.IsNullOrWhiteSpace(line))
                                                    {
                                                        sb.AppendLine($"  | {line.Trim()}");
                                                    }
                                                }
                                            }
                                            sb.AppendLine();
                                        }
                                    }
                                }
                            }

                            if (Logger.IsDebugEnabled)
                            {
                                foreach (IssueRecord? result in results)
                                {
                                    Logger.Debug($"Result: {result.Filename} {result.Issue.Rule.Name} {result.TextSample}");
                                }
                            }
                        }
                        Console.WriteLine(sb.ToString());
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                        Logger.Warn(ex.StackTrace);
                    }
                }
            }
            else
            {
                Logger.Warn("No target provided; nothing to analyze.");
                DetectCryptographyTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        public DetectCryptographyTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public DetectCryptographyTool() : this(new ProjectManagerFactory())
        {
        }

        /// <summary>
        /// Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl">The package-url of the package to analyze.</param>
        /// <returns>List of tags identified</returns>
        public async Task<List<IssueRecord>> AnalyzePackage(PackageURL purl, string? targetDirectoryName, bool doCaching)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());

            PackageDownloader? packageDownloader = new(purl, ProjectManagerFactory, targetDirectoryName, doCaching);
            List<string>? directoryNames = await packageDownloader.DownloadPackageLocalCopy(purl, false, true);
            directoryNames = directoryNames.Distinct().ToList<string>();

            List<IssueRecord>? analysisResults = new List<IssueRecord>();
            if (directoryNames.Count > 0)
            {
                foreach (string? directoryName in directoryNames)
                {
                    Logger.Trace("Analyzing directory {0}", directoryName);
                    List<IssueRecord>? singleResult = await AnalyzeDirectory(directoryName);
                    if (singleResult != null)
                    {
                        analysisResults.AddRange(singleResult);
                    }
                }
            }
            else
            {
                Logger.Warn("Error downloading {0}.", purl.ToString());
            }
            packageDownloader.ClearPackageLocalCopyIfNoCaching();
            packageDownloader.DeleteDestinationDirectoryIfTemp();
            return analysisResults.ToList();
        }

        private string NormalizeFileContent(string filename, byte[] buffer)
        {
            Logger.Trace("NormalizeFileContent({0}, {1}", filename, buffer?.Length);

            if (buffer == null || buffer.Length == 0)
            {
                return string.Empty;
            }

            // Check if we have a binary file - meaning more than 10% control characters
            // TODO: Is this the right metric?
            string? bufferString = Encoding.ASCII.GetString(buffer);
            int countControlChars = bufferString.Count(ch => char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t');
            bool isBinaryFile = (((double)countControlChars / (double)bufferString.Length) > 0.10);

            if (isBinaryFile)
            {
                // First, is it a PE file?
                if (PeFile.IsPeFile(buffer))
                {
                    try
                    {
                        CSharpDecompiler? decompiler = new CSharpDecompiler(filename, new DecompilerSettings());
                        return decompiler.DecompileWholeModuleAsString();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Unable to decompile {0}: {1}", filename, ex.Message);
                    }
                }

                HashSet<string>? resultStrings = new HashSet<string>();

                try
                {
                    // Do a full disassembly, but only show unique lines.
                    Disassembler.Translator.IncludeAddress = false;
                    Disassembler.Translator.IncludeBinary = false;
                    ArchitectureMode architecture = buffer.Length > 5 && buffer[5] == 0x02 ? ArchitectureMode.x86_64 : ArchitectureMode.x86_32;
                    using Disassembler? disassembler = new Disassembler(buffer, architecture);
                    foreach (SharpDisasm.Instruction? instruction in disassembler.Disassemble())
                    {
                        resultStrings.Add(instruction.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Unable to decompile {0}: {1}", filename, ex.Message);
                }

                try
                {
                    // Maybe it's WebAseembly -- @TODO Make this less random.
                    using MemoryStream? webAssemblyByteStream = new MemoryStream(buffer);

                    WebAssembly.Module? m = WebAssembly.Module.ReadFromBinary(webAssemblyByteStream);

                    foreach (Data? data in m.Data)
                    {
                        resultStrings.Add(Encoding.ASCII.GetString(data.RawData.ToArray()));
                    }

                    foreach (FunctionBody? functionBody in m.Codes)
                    {
                        foreach (WebAssembly.Instruction? instruction in functionBody.Code)
                        {
                            switch (instruction.OpCode)
                            {
                                case OpCode.Int32Constant:
                                    resultStrings.Add(((Int32Constant)instruction).Value.ToString());
                                    break;

                                case OpCode.Int64Constant:
                                    resultStrings.Add(((Int64Constant)instruction).Value.ToString());
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                    return string.Join('\n', resultStrings);
                }
                catch (WebAssembly.ModuleLoadException)
                {
                    // OK to ignore
                }
                catch (Exception ex)
                {
                    Logger.Warn("Unable to analyze WebAssembly {0}: {1}", filename, ex.Message);
                }

                if (resultStrings.Any())
                {
                    return string.Join('\n', resultStrings);
                }

                return string.Join('\n', UniqueStringsFromBinary(buffer));
            }

            // Fallback, just return the string iteself
            return bufferString;
        }

        public RuleSet GetEmbeddedRules()
        {
            RuleSet rules = new RuleSet(null);

            Assembly? assembly = Assembly.GetExecutingAssembly();
            foreach (string? resourceName in assembly.GetManifestResourceNames())
            {
                if (resourceName.EndsWith(".json"))
                {
                    try
                    {
                        Stream? stream = assembly.GetManifestResourceStream(resourceName);
                        using StreamReader? resourceStream = new StreamReader(stream ?? new MemoryStream());
                        rules.AddString(resourceStream.ReadToEnd(), resourceName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error loading {0}: {1}", resourceName, ex.Message);
                    }
                }
            }

            return rules;
        }
        
        /// <summary>
        /// Analyzes a directory of files.
        /// </summary>
        /// <param name="directory">directory to analyze.</param>
        /// <returns>List of tags identified</returns>
        public async Task<List<IssueRecord>> AnalyzeDirectory(string directory)
        {
            Logger.Trace("AnalyzeDirectory({0})", directory);

            List<IssueRecord>? analysisResults = new List<IssueRecord>();

            RuleSet rules = new RuleSet();
            if (Options["disable-default-rules"] is false)
            {
                rules.AddRange(GetEmbeddedRules());
                
                // Add Application Inspector cryptography rules
                var assembly = typeof(AnalyzeCommand).Assembly;
                foreach (string? resourceName in assembly.GetManifestResourceNames())
                {
                    if (resourceName.EndsWith(".json") && resourceName.Contains("cryptography"))
                    {
                        try
                        {
                            Stream? stream = assembly.GetManifestResourceStream(resourceName);
                            using StreamReader? resourceStream = new StreamReader(stream ?? new MemoryStream());
                            rules.AddString(resourceStream.ReadToEnd(), resourceName);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "Error loading {0}: {1}", resourceName, ex.Message);
                        }
                    }
                }
            }

            if (Options["custom-rule-directory"] is string customDirectory)
            {
                rules.AddDirectory(customDirectory);
            }

            if (!rules.Any())
            {
                Logger.Error("No rules were specified, unable to continue.");
                return analysisResults; // empty
            }
            RuleProcessor processor = new RuleProcessor(rules, new RuleProcessorOptions());

            string[] fileList;

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
                return analysisResults; // empty
            }

            foreach (string? filename in fileList)
            {
                Logger.Trace($"Processing {filename}");

                // TODO: Make this more resilient
                if (IGNORE_FILES.Contains(Path.GetFileName(filename)))
                {
                    Logger.Trace($"Ignoring {filename}");
                    continue;
                }

                byte[] fileContents;
                try
                {
                    fileContents = File.ReadAllBytes(filename);
                }
                catch (Exception ex)
                {
                    Logger.Trace(ex, "File {0} cannot be read, ignoring.", filename);
                    continue;
                }

                string? buffer = NormalizeFileContent(filename, fileContents);
                Logger.Debug("Normalization complete.");

                double MIN_CRYPTO_OP_DENSITY = 0.10;
                try
                {
                    // TODO don't do this if we disassembled native code
                    double cryptoOperationLikelihood = CalculateCryptoOpDensity(buffer);
                    Logger.Debug("Cryptographic operation density for {0} was {1}", filename, cryptoOperationLikelihood);

                    if (cryptoOperationLikelihood >= MIN_CRYPTO_OP_DENSITY)
                    {
                        analysisResults.Add(new IssueRecord(
                            Filename: filename,
                            Filesize: buffer.Length,
                            TextSample: "n/a",
                            Issue: new Issue(
                                Boundary: new Boundary(),
                                StartLocation: new Location(),
                                EndLocation: new Location(),
                                Rule: new Rule()
                                {
                                    Id = "_CRYPTO_DENSITY",
                                    Name = "Cryptographic symbols",
                                    Description = cryptoOperationLikelihood.ToString(),
                                    Tags = new string[]
                                    {
                                        "Cryptography.GenericImplementation.HighDensityOperators"
                                    }
                                }
                            )
                        ));
                    }
                    Logger.Debug($"Analyzing {filename}, length={buffer.Length}");

                    List<MatchRecord>? fileResults = null;
                    //processor.AnalyzeFile
                    FileEntry? holderEntry = new FileEntry("placeholder", new MemoryStream(Encoding.UTF8.GetBytes(buffer)));
                    LanguageInfo languageInfo = new LanguageInfo();
                    var languages = new Languages();
                    languages.FromFileName(filename, ref languageInfo);
                    Task<List<MatchRecord>>? task = Task.Run(() => processor.AnalyzeFile(holderEntry,languageInfo));
                    if (task.Wait(TimeSpan.FromSeconds(30)))
                    {
                        fileResults = task.Result;
                    }
                    else
                    {
                        Logger.Warn("DevSkim operation timed out.");
                        return analysisResults;
                    }

                    Logger.Debug("Operation Complete: {0}", fileResults?.Count);
                    foreach (MatchRecord? issue in fileResults ?? new List<MatchRecord>())
                    {
                        string[]? fileContentArray = buffer.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                        List<string>? excerpt = new List<string>();
                        int startLoc = Math.Max(issue.StartLocationLine - 1, 0);
                        int endLoc = Math.Min(issue.EndLocationLine + 1, fileContentArray.Length - 1);
                        for (int i = startLoc; i <= endLoc; i++)
                        {
                            excerpt.Add(fileContentArray[i].Trim());
                        }

                        analysisResults.Add(new IssueRecord(
                            Filename: filename,
                            Filesize: buffer.Length,
                            TextSample: issue.StartLocationLine + " => " + string.Join(Environment.NewLine, excerpt),
                            Issue: new Issue(
                                issue.Boundary, 
                                new Location() 
                                { 
                                    Column = issue.StartLocationColumn, 
                                    Line = issue.StartLocationLine 
                                }, 
                                new Location() 
                                { 
                                    Column = issue.EndLocationColumn, 
                                    Line = issue.EndLocationLine 
                                }, 
                                issue.Rule)
                            )
                        );
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error analyzing {0}: {1}", filename, ex.Message);
                    Logger.Warn(ex.StackTrace);
                }
            }

            return analysisResults;
        }

        /// <summary>
        /// Calculates the density of cryptographic operators within the buffer.
        /// </summary>
        /// <param name="buffer">Buffer to analyze</param>
        /// <param name="windowSize">Size of the window to use</param>
        /// <returns>Ratio (0-1) of the most dense windowSize characters of the buffer.</returns>
        private double CalculateCryptoOpDensity(string buffer, int windowSize = 50)
        {
            Logger.Trace("CalculateCryptoOpDensity()");

            if (string.IsNullOrWhiteSpace(buffer))
            {
                return 0;
            }
            int MIN_BUFFER_LENGTH = 5;

            // Consolidate whitespace
            buffer = Regex.Replace(buffer, "[\t ]", "");

            // Condense all multi-character strings into a single character
            buffer = Regex.Replace(buffer, "[a-zA-Z\\$_][a-zA-Z0-9_]*", "X");

            // If we have a very short string, ignore it.
            if (buffer.Distinct<char>().Count() < MIN_BUFFER_LENGTH)
            {
                return 0;
            }

            // Normalize the sliding window size
            windowSize = windowSize >= buffer.Length ? buffer.Length : windowSize;
            windowSize = windowSize <= 0 ? 50 : windowSize;

            // This is a horrible regular expression, but the intent is to capture symbol characters
            // that probably mean something within cryptographic code, but not within other code.
            Regex? cryptoChars = new Regex("(?<=[a-z0-9_])\\^=?(?=[a-z_])|(?<=[a-z0-9_])(>{2,3}|<{2,3})=?(?=[a-z0-9])|(?<=[a-z0-9_])([&^~|])=?(?=[a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // We report on the highest ratio of symbol to total characters
            double maxRatio = 0;

            // Iterate through all windows: TODO We can skip-count a few here
            for (int i = 0; i < buffer.Length - windowSize; i += 1)
            {
                string? windowBuffer = buffer.Substring(i, windowSize);
                windowBuffer = cryptoChars.Replace(windowBuffer, string.Empty);
                double ratio = ((double)windowSize - (double)windowBuffer.Length) / (double)windowSize;
                if (ratio > maxRatio)
                {
                    maxRatio = ratio;
                    Logger.Trace("Updated from: {0}", buffer.Substring(i, windowSize));
                    Logger.Trace("          to: {0}", windowBuffer);
                }
            }
            return maxRatio;
        }

        /// <summary>
        /// Extract unique strings (alphabetic) from a string
        /// TODO: This is ASCII-only.
        /// </summary>
        /// <param name="buffer">string to scan</param>
        /// <returns>unique strings</returns>
        private IEnumerable<string> UniqueStringsFromBinary(byte[] buffer)
        {
            string? bufferString = Encoding.ASCII.GetString(buffer);
            MatchCollection? words = Regex.Matches(bufferString, @"([a-zA-Z]\.[a-zA-Z ]{4,})");
            return words.Select(m => m.Value).Distinct<string>();
        }

        /// <summary>
        /// Parses options for this program.
        /// </summary>
        /// <param name="args">arguments (passed in from the user)</param>
        private void ParseOptions(string[] args)
        {
            if (args == null)
            {
                ShowUsage();
                Environment.Exit(1);
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-h":
                    case "--help":
                        ShowUsage();
                        Environment.Exit(1);
                        break;

                    case "-v":
                    case "--version":
                        Console.WriteLine($"{ToolName} {ToolVersion}");
                        Environment.Exit(1);
                        break;

                    case "--verbose":
                        Options["verbose"] = true;
                        break;

                    case "--custom-rule-directory":
                        Options["custom-rule-directory"] = args[++i];
                        break;

                    case "--disable-default-rules":
                        Options["disable-default-rules"] = true;
                        break;

                    case "--download-directory":
                        Options["download-directory"] = args[++i];
                        break;

                    case "--use-cache":
                        Options["use-cache"] = true;
                        break;

                    default:
                        if (Options["target"] is IList<string> targetList)
                        {
                            targetList.Add(args[i]);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Displays usage information for the program.
        /// </summary>
        private static void ShowUsage()
        {
            Console.WriteLine($@"
{ToolName} {ToolVersion}

Usage: {ToolName} [options] package-url...

positional arguments:
    package-url                 PackgeURL specifier to download (required, repeats OK), or directory.

{GetCommonSupportedHelpText()}

optional arguments:
  --verbose                     increase output verbosity
  --custom-rule-directory DIR   load rules from directory DIR
  --disable-default-rules       do not load default, built-in rules.
  --download-directory          the directory to download the package to
  --use-cache                   do not download the package if it is already present in the destination directory
  --format                      specify the output format (text|sarifv1|sarifv2). (default is text) (currently not supported)
  --output-file                 send the command output to a file instead of stdout (currently not supported)
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }

    public class IssueRecord
    {
        public string Filename { get; }
        public int Filesize { get; }
        public string TextSample { get; }
        public Issue Issue { get; }

        public IssueRecord(string Filename, int Filesize, string TextSample, Issue Issue)
        {
            this.Filename = Filename;
            this.Filesize = Filesize;
            this.TextSample = TextSample;
            this.Issue = Issue;
        }
    }
}