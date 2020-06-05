// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PeNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.DevSkim;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using System.Text.RegularExpressions;
using System.Reflection;
using SharpDisasm;
using WebAssembly; // Acquire from https://www.nuget.org/packages/WebAssembly
using WebAssembly.Instructions;

namespace Microsoft.CST.OpenSource
{
    public class DetectCryptographyTool : OSSGadget
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-detect-cryptography";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(DetectCryptographyTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

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
        static async Task Main(string[] args)
        {
            var detectCryptographyTool = new DetectCryptographyTool();
            Logger.Info($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");

            detectCryptographyTool.ParseOptions(args);

            if (detectCryptographyTool.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var target in targetList)
                {
                    sb.Clear();
                    try
                    {
                        List<IssueRecord>? results = null;
                        if (target.StartsWith("pkg:", StringComparison.InvariantCulture))
                        {
                            var purl = new PackageURL(target);
                            results = await detectCryptographyTool.AnalyzePackage(purl, 
                                (string?)detectCryptographyTool.Options["download-directory"], 
                                (bool?)detectCryptographyTool.Options["use-cache"] == true);
                        }
                        else if (Directory.Exists(target))
                        {
                            results = await detectCryptographyTool.AnalyzeDirectory(target);
                        }
                        else if (File.Exists(target))
                        {
                            string? targetDirectoryName = null;
                            while (targetDirectoryName == null || Directory.Exists(targetDirectoryName))
                            {
                                targetDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                            }
                            var projectManager = ProjectManagerFactory.CreateBaseProjectManager(targetDirectoryName);
                            
                            #pragma warning disable SCS0018 // Path traversal: injection possible in {1} argument passed to '{0}'
                            var path = await projectManager.ExtractArchive("temp", File.ReadAllBytes(target));
                            #pragma warning restore SCS0018 // Path traversal: injection possible in {1} argument passed to '{0}'
                            
                            results = await detectCryptographyTool.AnalyzeDirectory(path);
                            
                            // Clean up after ourselves
                            try
                            {
                                if (targetDirectoryName != null)
                                {
                                    Directory.Delete(targetDirectoryName, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn("Unable to delete {0}: {1}", targetDirectoryName, ex.Message);
                            }
                        }
                        
                        if (results == null)
                        {
                            Logger.Warn("Error generating results, was null.");
                        }
                        else if (!results.Any())
                        {
                            sb.AppendLine($"[ ] {target} - This software package does NOT appear to implement cryptography.");
                        }
                        else
                        {
                            var shortTags = results.SelectMany(r => r.Issue.Rule.Tags)
                                                   .Distinct()
                                                   .Where (t => t.StartsWith("Cryptography.Implementation."))
                                                   .Select(t => t.Replace("Cryptography.Implementation.", ""));
                            var otherTags = results.SelectMany(r => r.Issue.Rule.Tags)
                                                   .Distinct()
                                                   .Where(t => !t.StartsWith("Cryptography.Implementation."));

                            if (shortTags.Count() > 0)
                            {
                                sb.AppendLine($"[X] {target} - This software package appears to implement {string.Join(", ", shortTags)}.");
                            }

                            if (otherTags.Contains("Cryptography.GenericImplementation.HighDensityOperators"))
                            {
                                sb.AppendLine($"[X] {target} - This software package has a high-density of cryptographic operators.");
                            }
                            else
                            {
                                sb.AppendLine($"[ ] {target} - This software package does NOT have a high-density of cryptographic operators.");
                            }

                            if (otherTags.Contains("Cryptography.GenericImplementation.CryptographicWords"))
                            {
                                sb.AppendLine($"[X] {target} - This software package contains words that suggest cryptography.");
                            }
                            else
                            {
                                sb.AppendLine($"[ ] {target} - This software package does NOT contains words that suggest cryptography.");
                            }
                            
                            if ((bool?)detectCryptographyTool.Options["verbose"] == true)
                            {
                                var items = results.GroupBy(k => k.Issue.Rule.Name).OrderByDescending(k => k.Count());
                                foreach (var item in items)
                                {
                                    sb.AppendLine();
                                    sb.AppendLine($"There were {item.Count()} finding(s) of type [{item.Key}].");
                                    
                                    foreach (var result in results)
                                    {
                                        if (result.Issue.Rule.Name == item.Key)
                                        {
                                            sb.AppendLine($" {result.Filename}:");
                                            if (result.Issue.Rule.Id == "_CRYPTO_DENSITY")
                                            {
                                                // No excerpt for cryptogrpahic density
                                                // TODO: We stuffed the density in the unused 'Description' field. This is code smell.
                                                sb.AppendLine($"  | The maximum cryptographic density is {result.Issue.Rule.Description}.");
                                            }
                                            else
                                            {
                                                // Show the excerpt
                                                foreach (var line in result.TextSample.Split(new char[] { '\n', '\r' }))
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
                                foreach (var result in results)
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

        public DetectCryptographyTool() : base()
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

            var packageDownloader = new PackageDownloader(purl, targetDirectoryName, doCaching);
            var directoryNames = await packageDownloader.DownloadPackageLocalCopy(purl, false, true);
            directoryNames = directoryNames.Distinct().ToList<string>();

            var analysisResults = new List<IssueRecord>();
            if (directoryNames.Count > 0)
            {
                foreach (var directoryName in directoryNames)
                {
                    Logger.Trace("Analyzing directory {0}", directoryName);
                    var singleResult = await AnalyzeDirectory(directoryName);
                    if (singleResult != null)
                    {
                        analysisResults.AddRange(singleResult);
                    }

                    Logger.Trace("Removing directory {0}", directoryName);
                    try
                    {
                        Directory.Delete(directoryName, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error removing {0}: {1}", directoryName, ex.Message);
                    }
                }
            }
            else
            {
                Logger.Warn("Error downloading {0}.", purl.ToString());
            }
            packageDownloader.ClearPackageLocalCopyIfNoCaching();

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
            var bufferString = Encoding.ASCII.GetString(buffer);
            var countControlChars = bufferString.Count(ch => char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t');
            var isBinaryFile = (((double)countControlChars / (double)bufferString.Length) > 0.10);

            if (isBinaryFile)
            {
                // First, is it a PE file?
                if (PeFile.IsPeFile(buffer))
                {
                    try
                    {
                        var decompiler = new CSharpDecompiler(filename, new DecompilerSettings());
                        return decompiler.DecompileWholeModuleAsString();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Unable to decompile {0}: {1}", filename, ex.Message);
                    }
                }
                
                var resultStrings = new HashSet<string>();

                try
                {
                    // Do a full disassembly, but only show unique lines.
                    Disassembler.Translator.IncludeAddress = false;
                    Disassembler.Translator.IncludeBinary = false;
                    var architecture = buffer.Length > 5 && buffer[5] == 0x02 ? ArchitectureMode.x86_64 : ArchitectureMode.x86_32;
                    using var disassembler = new Disassembler(buffer, architecture);
                    foreach (var instruction in disassembler.Disassemble())
                    {
                        resultStrings.Add(instruction.ToString());
                    }
                    
                }
                catch(Exception ex)
                {
                    Logger.Warn("Unable to decompile {0}: {1}", filename, ex.Message);
                }

                try
                {
                    // Maybe it's WebAseembly -- @TODO Make this less random.
                    using var webAssemblyByteStream = new MemoryStream(buffer);
                    var m = WebAssembly.Module.ReadFromBinary(webAssemblyByteStream);

                    foreach (var data in m.Data)
                    {
                        resultStrings.Add(Encoding.ASCII.GetString(data.RawData.ToArray()));
                    }

                    foreach (var functionBody in m.Codes)
                    {
                        foreach (var instruction in functionBody.Code)
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
                catch(Exception ex)
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

        /// <summary>
        /// Analyzes a directory of files.
        /// </summary>
        /// <param name="directory">directory to analyze.</param>
        /// <returns>List of tags identified</returns>
        public async Task<List<IssueRecord>> AnalyzeDirectory(string directory)
        {
            Logger.Trace("AnalyzeDirectory({0})", directory);

            var analysisResults = new List<IssueRecord>();

            RuleSet rules = new RuleSet();
            if (Options["disable-default-rules"] is bool disableDefaultRules && !disableDefaultRules)
            {
                var assembly = Assembly.GetExecutingAssembly();
                foreach (var resourceName in assembly.GetManifestResourceNames())
                {
                    if (resourceName.EndsWith(".json"))
                    {
                        try
                        {
                            var stream = assembly.GetManifestResourceStream(resourceName);
                            using var resourceStream = new StreamReader(stream ?? new MemoryStream());
                            rules.AddString(resourceStream.ReadToEnd(), resourceName);
                        }
                        catch(Exception ex)
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

            if (rules.Count() == 0)
            {
                Logger.Error("No rules were specified, unable to continue.");
                return analysisResults; // empty
            }

            var processor = new RuleProcessor(rules)
            {
                EnableSuppressions = false,
                SeverityLevel = (Severity)31
            };

            foreach (var filename in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
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
                } catch(Exception ex)
                {
                    Logger.Trace(ex, "File {0} cannot be read, ignoring.", filename);
                    continue;
                }

                var buffer = NormalizeFileContent(filename, fileContents);
                Logger.Debug("Normalization complete.");

                double MIN_CRYPTO_OP_DENSITY = 0.10;
                try
                {
                    // TODO don't do this if we disassembled native code
                    var cryptoOperationLikelihood = CalculateCryptoOpDensity(buffer);
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
                                Rule: new Rule("Crypto Symbols")
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
                    
                    Issue[]? fileResults = null;
                    var task = Task.Run(() => processor.Analyze(buffer, Language.FromFileName(filename)));
                    if (task.Wait(TimeSpan.FromSeconds(2)))
                    {
                        fileResults = task.Result;
                    }
                    else
                    {
                        Logger.Debug("DevSkim operation timed out.");
                        return analysisResults;
                    }

                    Logger.Debug("Operation Complete: {0}", fileResults?.Length);
                    foreach (var issue in fileResults ?? Array.Empty<Issue>())
                    {
                        var fileContentArray = buffer.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                        var excerpt = new List<string>();
                        var startLoc = Math.Max(issue.StartLocation.Line - 1, 0);
                        var endLoc = Math.Min(issue.EndLocation.Line + 1, fileContentArray.Length - 1);
                        for (int i=startLoc; i<=endLoc; i++)
                        {
                            excerpt.Add(fileContentArray[i].Trim());
                        }

                        analysisResults.Add(new IssueRecord(
                            Filename: filename,
                            Filesize: buffer.Length,
                            TextSample: issue.StartLocation.Line + " => " + string.Join(Environment.NewLine, excerpt),
                            Issue: issue)
                        );
                    }
                }
                catch(Exception ex)
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

            // This is a horrible regular expression, but the intent is to capture symbol characters that
            // probably mean something within cryptographic code, but not within other code.
            var cryptoChars = new Regex("(?<=[a-z0-9_])\\^=?(?=[a-z_])|(?<=[a-z0-9_])(>{2,3}|<{2,3})=?(?=[a-z0-9])|(?<=[a-z0-9_])([&^~|])=?(?=[a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // We report on the highest ratio of symbol to total characters
            double maxRatio = 0;

            // Iterate through all windows: TODO We can skip-count a few here
            for (int i = 0; i < buffer.Length - windowSize; i += 1)
            {
                var windowBuffer = buffer.Substring(i, windowSize);
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
            var bufferString = Encoding.ASCII.GetString(buffer);
            var words = Regex.Matches(bufferString, @"([a-zA-Z]\.[a-zA-Z ]{4,})");
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
                        Console.Error.WriteLine($"{TOOL_NAME} {VERSION}");
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
            Console.Error.WriteLine($@"
{TOOL_NAME} {VERSION}

Usage: {TOOL_NAME} [options] package-url...

positional arguments:
    package-url                 PackgeURL specifier to download (required, repeats OK)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --verbose                     increase output verbosity
  --custom-rule-directory DIR   load rules from directory DIR
  --disable-default-rules       do not load default, built-in rules.
  --download-directory          the directory to download the package to
  --use-cache                   do not download the package if it is already present in the destination directory
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

