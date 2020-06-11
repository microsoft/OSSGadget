// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
using MimeTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    internal class DefoggerTool : OSSGadget
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-defogger";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(DefoggerTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>
        /// Regular expression that matches Base64-encoded text.
        /// </summary>
        private static readonly Regex BASE64_REGEX = new Regex("(([A-Z0-9+\\/]{4})+([A-Z0-9+\\/]{3}=|[A-Z0-9+\\/]{2}==)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        /// Regular expression that matches hex-encoded text.
        /// </summary>
        private static readonly Regex HEX_REGEX = new Regex(@"(0x)?([A-F0-9]{16,})", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Short strings must match this regular expression to be reported.
        /// </summary>
        private static readonly Regex SHORT_INTERESTING_STRINGS_REGEX = new Regex(@"^[A-Z0-9\-:]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        /// Do not analyze binary files (those with a MIME type that matches this regular expression).
        /// </summary>
        private static readonly Regex IGNORE_MIME_REGEX = new Regex(@"audio|video|x-msdownload", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        /// Only report detected strings this length or longer.
        /// </summary>
        private const int DEFAULT_MINIMUM_STRING_LENGTH = 8;

        /// <summary>
        /// Strings longer than this are interesting.
        /// </summary>
        private const int INTERESTING_STRINGS_CUTOFF = 24;

        /// <summary>
        /// Command line options passed into this tool.
        /// </summary>
        private readonly IDictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "download-directory", null },
            { "use-cache", false }
        };

        /// <summary>
        /// Identified findings from the analysis.
        /// </summary>
        public IList<EncodedString> Findings { get; private set; }

        /// <summary>
        /// The specific type of encoding detected.
        /// </summary>
        public enum EncodedStringType
        {
            Base64,
            Hex
        }

        /// <summary>
        /// The encoded string and type.
        /// </summary>
        public class EncodedString
        {
            public EncodedStringType Type;
            public string Filename;
            public string EncodedText;
            public string DecodedText;

            public EncodedString(EncodedStringType Type, string Filename, string EncodedText, string DecodedText)
            {
                this.Type = Type;
                this.Filename = Filename;
                this.EncodedText = EncodedText;
                this.DecodedText = DecodedText;
            }
        }

        private static void Main(string[] args)
        {
            CommonInitialization.Initialize();

            Logger?.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");

            var defoggerTool = new DefoggerTool();
            defoggerTool.ParseOptions(args);

            if (defoggerTool.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                foreach (var target in targetList)
                {
                    try
                    {
                        if (target.StartsWith("pkg:"))
                        {
                            var purl = new PackageURL(target);
                            defoggerTool.AnalyzePackage(purl,
                                (string?)defoggerTool.Options["download-directory"],
                                (bool?)defoggerTool.Options["use-cache"] == true).Wait();
                        }
                        else if (Directory.Exists(target))
                        {
                            defoggerTool.AnalyzeDirectory(target);
                        }
                        else
                        {
                            throw new Exception($"Invalid target: [{target}]");
                        }

                        foreach (var finding in defoggerTool.Findings)
                        {
                            Logger?.Info("{0}: {1} -> {2}", finding.Filename, finding.EncodedText, finding.DecodedText);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.Warn(ex, "Unable to analyze {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger?.Warn("No target provided; nothing to analyze.");
                DefoggerTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Initializes a new DefoggerTool instance.
        /// </summary>
        public DefoggerTool() : base()
        {
            Findings = new List<EncodedString>();
        }

        /// <summary>
        /// Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl">The package-url of the package to analyze.</param>
        /// <returns>n/a</returns>
        public async Task AnalyzePackage(PackageURL purl, string? destinationDirectory, bool doCaching)
        {
            Logger?.Trace("AnalyzePackage({0})", purl.ToString());

            var packageDownloader = new PackageDownloader(purl, destinationDirectory, doCaching);
            foreach (var directory in await packageDownloader.DownloadPackageLocalCopy(purl, false, true))
            {
                AnalyzeDirectory(directory);
            }

            packageDownloader.ClearPackageLocalCopyIfNoCaching();
        }

        /// <summary>
        /// Analyzes a directory of files.
        /// </summary>
        /// <param name="directory">directory to analyze.</param>
        public void AnalyzeDirectory(string directory)
        {
            Logger?.Trace("AnalyzeDirectory({0})", directory);
            var fileList = Directory.EnumerateFiles(directory, @"*.*", SearchOption.AllDirectories);
            foreach (var filename in fileList)
            {
                try
                {
                    AnalyzeFile(filename);
                }
                catch (Exception ex)
                {
                    Logger?.Warn("Error processing file [{0}]: {1}", filename, ex.Message);
                }
            }
        }

        /// <summary>
        /// Analyzes a single file.
        /// </summary>
        /// <param name="filename">filename to analyze</param>
        public void AnalyzeFile(string filename)
        {
            Logger?.Trace("AnalyzeFile({0})", filename);

            var mimeType = MimeTypeMap.GetMimeType(Path.GetExtension(filename));
            if (IGNORE_MIME_REGEX.IsMatch(mimeType))
            {
                Logger?.Debug("Ignoring {0}; invalid MIME type: {1}", filename, mimeType);
                return;
            }

#pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
            var fileContents = File.ReadAllText(filename);
#pragma warning restore SEC0116 // Path Tampering Unvalidated File Path

            foreach (Match match in BASE64_REGEX.Matches(fileContents).Where(match => match != null))
            {
                if (!match.Success)
                {
                    continue;
                }

                // Try to decode and then re-encode. Are we successful, and do we get the same value
                // we started out with? This will filter out Base64-encoded binary data, which is
                // what we want.
                try
                {
                    var bytes = Convert.FromBase64String(match.Value);
                    var decoded = Encoding.UTF8.GetString(bytes);

                    // Bail out early if the decoded string isn't interesting.
                    if (!IsInterestingString(decoded))
                    {
                        continue;
                    }

                    // Does the re-encoded string match?
                    var reencoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded));
                    if (match.Value.Equals(reencoded))
                    {
                        Findings.Add(new EncodedString(
                            Filename: filename,
                            EncodedText: match.Value,
                            DecodedText: decoded,
                            Type: EncodedStringType.Base64
                        ));
                    }
                }
                catch (Exception ex)
                {
                    // No action needed
                    Logger?.Trace("Invalid match for {0}: {1}", match.Value, ex.Message);
                }
            }

            foreach (Match match in HEX_REGEX.Matches(fileContents).Where(match => match != null))
            {
                var decodedText = HexToString(match.Value);

                // Bail out if the decoded string isn't interesting
                if (!IsInterestingString(decodedText))
                {
                    continue;
                }
                // We don't need to do the dance we did above because all hex strings are valid encodings.
                Findings.Append(new EncodedString(
                    Filename: filename,
                    EncodedText: match.Value,
                    DecodedText: decodedText,
                    Type: EncodedStringType.Hex
                ));
            }
        }

        private static string HexToString(String hex)
        {
            //Logger?.Trace("HexToString({0})", hex);

            hex = hex.Trim();
            var stringLength = hex.Length;

            // If we have an odd string length, like ABC, then we really mean 0ABC.
            if (stringLength % 2 == 1)
            {
                hex = "0" + hex;
                stringLength++;
            }

            // Remove an initial '0x' prefix
            if (hex.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                hex = hex.Substring(2);
                stringLength -= 2;
            }

            // Store enough bytes for half the length (hex digits are 4 bits)
            byte[] bytes = new byte[stringLength / 2];
            for (var i = 0; i < stringLength; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            // Convert the bytes back into a UTF8 string
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Decides whether the given string is interesting or not
        /// </summary>
        /// <param name="s">string to check</param>
        /// <returns></returns>
        private static bool IsInterestingString(string s)
        {
            //Logger?.Trace("IsInterestingString({0})", s);
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            // Standard ignores (false positives)
            if ((s.Contains("\"version\"") &&
                    (s.Contains("\"sourceRoot\"") || s.Contains("\"sourcesContent\"") || s.Contains("\"names\""))) || /* JavaScript Map files */
                 (s.StartsWith("<?xml version=\"1.0\"") && s.Contains("<svg ")) ||
                 (s.StartsWith("<svg ")) ||
                 (s.Contains("ReleaseAsset") && Regex.IsMatch(@"\d+:ReleaseAsset\d+", s)) ||
                 (s.Contains("Release") && Regex.IsMatch(@"\d+:Release\d+", s)))
            {
                //Logger?.Debug($"No, has tag: [{s}]");
                return false;
            }

            // If we're long enough and we pass the interesting string regex, then we're interesting
            if (s.Length >= DEFAULT_MINIMUM_STRING_LENGTH && SHORT_INTERESTING_STRINGS_REGEX.IsMatch(s))
            {
                return true;
            }
            // Otherwise, if we're too short, we're uninteresting
            if (s.Length < DEFAULT_MINIMUM_STRING_LENGTH)
            {
                //Logger?.Debug($"No, too short: [{s}]");
                return false;
            }
            // Otherwise, if we're pretty long, we're interesting
            if (s.Length > INTERESTING_STRINGS_CUTOFF)
            {
                return true;
            }
            // Otherwise, we're uninteresting
            //Logger?.Debug($"No, fall through: [{s}]");
            return false;
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
  package-url                   package url to analyze (required, multiple allowed)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --download-directory          the directory to download the package to
  --use-cache                   do not download the package if it is already present in the destination directory
  --help                        show this help message and exit
  --version                     show version number
");
        }
    }
}