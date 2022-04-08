// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
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
    using PackageManagers;
    using PackageUrl;

    public class DefoggerTool : OSSGadget
    {
        /// <summary>
        /// String with placeholders that matches Base64-encoded text.
        /// </summary>
        private static readonly string BASE64_REGEX_STRING = "(([A-Z0-9+\\/]{B})+(([A-Z0-9+\\/]{3}=)|([A-Z0-9+\\/]{2}==))?)";

        /// <summary>
        /// String with placeholders that matches hex-encoded text.
        /// </summary>
        private static readonly string HEX_REGEX_STRING = "(0x)?(([A-F0-9][A-F0-9]{B,})|(([A-F0-9][A-F0-9]-){C,}[A-F0-9][A-F0-9]))";

        /// <summary>
        /// Regular expression that matches Base64-encoded text.
        /// </summary>
        private static Regex BASE64_REGEX = new Regex(BASE64_REGEX_STRING.Replace("B", "4"), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        /// Regular expression that matches hex-encoded text.
        /// </summary>
        private static Regex HEX_REGEX = new Regex(HEX_REGEX_STRING.Replace("B", "8").Replace("C", "7"), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
        /// Enum of executable types
        /// </summary>
        public enum ExecutableType
        {
            Unknown,
            Windows,
            MacOS,
            Linux,
            None,
            Java
        }

        public static byte[] HexStringToBytes(string hex)
        {
            return Enumerable.Range(0, hex.Length / 2)
                             .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                             .ToArray();
        }

        /// <summary>
        /// Linux Elf
        /// </summary>
        public static readonly byte[] ElfMagicNumber = HexStringToBytes("7F454C46");

        /// <summary>
        /// Java Classes
        /// </summary>
        public static readonly byte[] JavaMagicNumber = HexStringToBytes("CAFEBABE");

        /// <summary>
        /// Mac Binary Magic numbers
        /// </summary>
        public static readonly List<byte[]> MacMagicNumbers = new List<byte[]>()
        {
            // 32 Bit Binary
            HexStringToBytes("FEEDFACE"),
            // 64 Bit Binary
            HexStringToBytes("FEEDFACF"),
            // 32 Bit Binary (reverse byte ordering)
            HexStringToBytes("CEFAEDFE"),
            // 64 Bit Binary (reverse byte ordering)
            HexStringToBytes("CFFAEDFE"),
            // "Fat Binary"
            HexStringToBytes("CAFEBABE")
        };

        /// <summary>
        /// Windows Binary header
        /// </summary>
        public static readonly byte[] WindowsMagicNumber = HexStringToBytes("4D5A");

        /// <summary>
        /// Gets the executable type of the given stream.
        /// </summary>
        /// <param name="input">Stream bytes to check</param>
        /// <returns>The executable type</returns>
        public static ExecutableType GetExecutableType(Stream input)
        {
            if (input == null) { return ExecutableType.Unknown; }
            if (input.Length < 4) { return ExecutableType.None; }

            byte[]? fourBytes = new byte[4];
            long initialPosition = input.Position;

            try
            {
                input.Read(fourBytes);
                input.Position = initialPosition;
            }
            catch (Exception e)
            {
                Logger.Debug("Couldn't chomp 4 bytes ({1}:{2})", e.GetType().ToString(), e.Message);
                return ExecutableType.Unknown;
            }

            switch (fourBytes)
            {
                case var span when span.SequenceEqual(ElfMagicNumber):
                    return ExecutableType.Linux;

                case var span when span.SequenceEqual(JavaMagicNumber):
                    return ExecutableType.Java;

                case var span when MacMagicNumbers.Contains(span):
                    return ExecutableType.MacOS;

                case var span when span[0..2].SequenceEqual(WindowsMagicNumber):
                    return ExecutableType.Windows;

                default:
                    return ExecutableType.None;
            }
        }

        /// <summary>
        /// Command line options passed into this tool.
        /// </summary>
        private readonly IDictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "download-directory", null },
            { "use-cache", false },
            { "save-found-binaries-to", null },
            { "save-archives-to", null },
            { "save-blobs-to", null },
            { "report-blobs", null },
            { "minimum-base64-length", 1 },
            { "minimum-hex-length", 8 }
        };

        /// <summary>
        /// Identified findings from the analysis.
        /// </summary>
        public IList<EncodedString> Findings { get; private set; }

        public List<EncodedBinary> BinaryFindings { get; }
        public List<EncodedArchive> ArchiveFindings { get; }
        public List<EncodedBlob> NonTextFindings { get; private set; }

        /// <summary>
        /// The specific type of encoding detected.
        /// </summary>
        public enum EncodedStringType
        {
            Base64,
            Hex,
            Compressed
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

        /// <summary>
        /// The encoded string and type.
        /// </summary>
        public class EncodedBlob
        {
            public string Filename;
            public string EncodedText;
            public string DecodedText;

            public EncodedBlob(string Filename, string EncodedText, string DecodedText)
            {
                this.Filename = Filename;
                this.EncodedText = EncodedText;
                this.DecodedText = DecodedText;
            }
        }

        /// <summary>
        /// The encoded archive and type.
        /// </summary>
        public class EncodedArchive
        {
            public ArchiveFileType Type;
            public string Filename;
            public string EncodedText;
            public Stream DecodedArchive;

            public EncodedArchive(ArchiveFileType Type, string Filename, string EncodedText, Stream DecodedArchive)
            {
                this.Type = Type;
                this.Filename = Filename;
                this.EncodedText = EncodedText;
                this.DecodedArchive = DecodedArchive;
            }
        }

        /// <summary>
        /// The encoded binary and type.
        /// </summary>
        public class EncodedBinary
        {
            public ExecutableType Type;
            public string Filename;
            public string EncodedText;
            public Stream DecodedBinary;

            public EncodedBinary(ExecutableType Type, string Filename, string EncodedText, Stream DecodedBinary)
            {
                this.Type = Type;
                this.Filename = Filename;
                this.EncodedText = EncodedText;
                this.DecodedBinary = DecodedBinary;
            }
        }

        private static async Task Main(string[] args)
        {
            ShowToolBanner();

            DefoggerTool? defoggerTool = new DefoggerTool();
            defoggerTool.ParseOptions(args);

            if (defoggerTool.Options["target"] is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        if (target.StartsWith("pkg:"))
                        {
                            PackageURL? purl = new PackageURL(target);
                            defoggerTool.AnalyzePackage(purl,
                                (string?)defoggerTool.Options["download-directory"],
                                (bool?)defoggerTool.Options["use-cache"] == true).Wait();
                        }
                        else if (System.IO.Directory.Exists(target))
                        {
                            defoggerTool.AnalyzeDirectory(target);
                        }
                        else
                        {
                            throw new Exception($"Invalid target: [{target}]");
                        }

                        foreach (EncodedString? finding in defoggerTool.Findings)
                        {
                            Logger.Info("[String] {0}: {1} -> {2}", finding.Filename, finding.EncodedText, finding.DecodedText);
                        }

                        string? binaryDir = (string?)defoggerTool.Options["save-found-binaries-to"];
                        int binaryNumber = 0;
                        foreach (EncodedBinary? binaryFinding in defoggerTool.BinaryFindings)
                        {
                            Logger.Info("[Binary] {0}: {1} -> {2}", binaryFinding.Filename, binaryFinding.EncodedText, binaryFinding.Type);
                            if (binaryDir is string)
                            {
                                System.IO.Directory.CreateDirectory(binaryDir);
                                string? path = Path.Combine(binaryDir, binaryFinding.Filename, $"binary-{binaryNumber}");
                                Logger.Info("Saving to {0}", path);
                                FileStream? fs = new FileStream(path, System.IO.FileMode.OpenOrCreate);
                                binaryFinding.DecodedBinary.CopyTo(fs);
                                binaryNumber++;
                            }
                        }

                        string? archiveDir = (string?)defoggerTool.Options["save-archives-to"];
                        int archiveNumber = 0;
                        foreach (EncodedArchive? archiveFinding in defoggerTool.ArchiveFindings)
                        {
                            Logger.Info("[Archive] {0}: {1} -> {2}", archiveFinding.Filename, archiveFinding.EncodedText, archiveFinding.Type);
                            if (archiveDir is string)
                            {
                                System.IO.Directory.CreateDirectory(archiveDir);
                                string? path = Path.Combine(archiveDir, archiveFinding.Filename, $"archive-{archiveNumber++}");
                                Logger.Info("Saving to {0}", path);
                                FileStream? fs = new FileStream(path, System.IO.FileMode.OpenOrCreate);
                                archiveFinding.DecodedArchive.CopyTo(fs);
                                archiveNumber++;
                            }
                        }

                        string? blobDir = (string?)defoggerTool.Options["save-blobs-to"];
                        int blobNumber = 0;
                        foreach (EncodedBlob? blobFinding in defoggerTool.NonTextFindings)
                        {
                            if (defoggerTool.Options["report-blobs"] is bool x && x is true)
                            {
                                Logger.Info("[Blob] {0}: {1}", blobFinding.Filename, blobFinding.EncodedText);
                            }
                            if (blobDir is string)
                            {
                                string? path = Path.Combine(blobDir, blobFinding.Filename, $"blob-{blobNumber++}");
                                Logger.Info("Saving to {0}", path);
                                File.WriteAllText(path, blobFinding.DecodedText);
                                blobNumber++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Unable to analyze {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger.Warn("No target provided; nothing to analyze.");
                DefoggerTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Initializes a new DefoggerTool instance.
        /// </summary>
        public DefoggerTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
            Findings = new List<EncodedString>();
            BinaryFindings = new List<EncodedBinary>();
            ArchiveFindings = new List<EncodedArchive>();
            NonTextFindings = new List<EncodedBlob>();
        }

        public DefoggerTool() : this(new ProjectManagerFactory())
        {
        }

        /// <summary>
        /// Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl">The package-url of the package to analyze.</param>
        /// <returns>n/a</returns>
        public async Task AnalyzePackage(PackageURL purl, string? destinationDirectory, bool doCaching)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());

            PackageDownloader? packageDownloader = new PackageDownloader(purl, ProjectManagerFactory, destinationDirectory, doCaching);
            foreach (string? directory in await packageDownloader.DownloadPackageLocalCopy(purl, false, true))
            {
                if (System.IO.Directory.Exists(directory))
                {
                    AnalyzeDirectory(directory);
                }
                else if (File.Exists(directory))
                {
                    AnalyzeFile(directory);
                }
                else
                {
                    Logger.Warn("{0} is neither a directory nor a file.", directory);
                }
            }

            packageDownloader.ClearPackageLocalCopyIfNoCaching();
        }

        /// <summary>
        /// Analyzes a directory of files.
        /// </summary>
        /// <param name="directory">directory to analyze.</param>
        public void AnalyzeDirectory(string directory)
        {
            Logger.Trace("AnalyzeDirectory({0})", directory);
            IEnumerable<string>? fileList = System.IO.Directory.EnumerateFiles(directory, @"*.*", SearchOption.AllDirectories);
            foreach (string? filename in fileList)
            {
                try
                {
                    AnalyzeFile(filename);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error processing file [{0}]: {1}", filename, ex.Message);
                }
            }
        }

        /// <summary>
        /// Analyzes a single file.
        /// </summary>
        /// <param name="filename">filename to analyze</param>
        public void AnalyzeFile(string filename)
        {
            Logger.Trace("AnalyzeFile({0})", filename);

            string? mimeType = MimeTypeMap.GetMimeType(Path.GetExtension(filename));
            if (IGNORE_MIME_REGEX.IsMatch(mimeType))
            {
                Logger.Debug("Ignoring {0}; invalid MIME type: {1}", filename, mimeType);
                return;
            }

#pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
            string? fileContents = File.ReadAllText(filename);
#pragma warning restore SEC0116 // Path Tampering Unvalidated File Path

            AnalyzeFile(filename, fileContents);
        }

        public bool HasNonTextContent(string content)
        {
            return content.Any(ch => char.IsControl(ch) && !char.IsWhiteSpace(ch));
        }

        public void AnalyzeFile(string filename, string fileContents)
        {
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
                    byte[]? bytes = Convert.FromBase64String(match.Value);
                    // Does the re-encoded string match?
                    string? reencoded = Convert.ToBase64String(bytes);

                    if (match.Value.Equals(reencoded))
                    {
                        FileEntry? entry = new FileEntry("bytes", new MemoryStream(bytes), new FileEntry(filename, new MemoryStream()));

                        ExecutableType exeType = GetExecutableType(entry.Content);

                        if (exeType is not ExecutableType.None && exeType is not ExecutableType.Unknown)
                        {
                            BinaryFindings.Add(new EncodedBinary(exeType, filename, match.Value, entry.Content));
                        }
                        else
                        {
                            ArchiveFileType archiveFileType = MiniMagic.DetectFileType(entry);

                            Extractor? extractor = new Extractor();

                            if (archiveFileType is not ArchiveFileType.UNKNOWN && archiveFileType is not ArchiveFileType.INVALID)
                            {
                                ArchiveFindings.Add(new EncodedArchive(archiveFileType, filename, match.Value, entry.Content));
                                foreach (FileEntry? extractedEntry in extractor.Extract(entry, new ExtractorOptions() { MemoryStreamCutoff = int.MaxValue }))
                                {
                                    exeType = GetExecutableType(extractedEntry.Content);
                                    if (exeType is not ExecutableType.None && exeType is not ExecutableType.Unknown)
                                    {
                                        BinaryFindings.Add(new EncodedBinary(exeType, filename, match.Value, entry.Content));
                                    }
                                    else
                                    {
                                        string? zippedString = new StreamReader(extractedEntry.Content).ReadToEnd();
                                        AnalyzeFile(extractedEntry.FullPath, zippedString);
                                    }
                                }
                            }
                            else
                            {
                                string? decoded = Encoding.Default.GetString(bytes);
                                if (HasNonTextContent(decoded))
                                {
                                    NonTextFindings.Add(new EncodedBlob(
                                        Filename: filename,
                                        EncodedText: match.Value,
                                        DecodedText: decoded
                                    ));

                                    AnalyzeFile(Path.Combine(filename, match.Value), decoded);
                                }
                                // Bail out early if the decoded string isn't interesting.
                                if (!IsInterestingString(decoded))
                                {
                                    continue;
                                }

                                Findings.Add(new EncodedString(
                                    Filename: filename,
                                    EncodedText: match.Value,
                                    DecodedText: decoded,
                                    Type: EncodedStringType.Base64
                                ));

                                AnalyzeFile(Path.Combine(filename, match.Value), decoded);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // No action needed
                    Logger.Trace("Invalid match for {0}: {1}", match.Value, ex.Message);
                }
            }

            foreach (Match match in HEX_REGEX.Matches(fileContents).Where(match => match != null))
            {
                string? decodedText = HexToString(match.Value);

                // Bail out if the decoded string isn't interesting
                if (!IsInterestingString(decodedText))
                {
                    continue;
                }
                // We don't need to do the dance we did above because all hex strings are valid encodings.
                Findings.Add(new EncodedString(
                    Filename: filename,
                    EncodedText: match.Value,
                    DecodedText: decodedText,
                    Type: EncodedStringType.Hex
                ));

                AnalyzeFile(Path.Combine(filename, match.Value), decodedText);
            }
        }

        /// <summary>
        /// Converts hex data to a string
        /// </summary>
        /// <param name="hex">The Hex data to decode</param>
        /// <returns>The decoded string</returns>
        private static string HexToString(string hex)
        {
            hex = hex.Trim();
            hex = hex.Replace("-", "");
            int stringLength = hex.Length;

            // Remove an initial '0x' prefix
            if (hex.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                hex = hex.Substring(2);
                stringLength -= 2;
            }

            // If we have an odd string length, like ABC, then we really mean 0ABC.
            if (stringLength % 2 == 1)
            {
                hex = "0" + hex;
                stringLength++;
            }
            // Store enough bytes for half the length (hex digits are 4 bits)
            byte[] bytes = new byte[stringLength / 2];
            for (int i = 0; i < stringLength; i += 2)
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
                return false;
            }
            // Otherwise, if we're pretty long, we're interesting
            if (s.Length > INTERESTING_STRINGS_CUTOFF)
            {
                return true;
            }
            // Otherwise, we're uninteresting
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
                        Console.WriteLine($"{GetToolName()} {GetToolVersion()}");
                        Environment.Exit(1);
                        break;

                    case "--download-directory":
                        Options["download-directory"] = args[++i];
                        break;

                    case "--use-cache":
                        Options["use-cache"] = true;
                        break;

                    case "--save-found-binaries-to":
                        Options["save-found-binaries-to"] = args[++i];
                        break;

                    case "--save-archives-to":
                        Options["save-archives-to"] = args[++i];
                        break;

                    case "--save-blobs-to":
                        Options["save-blobs-to"] = args[++i];
                        break;

                    case "--report-blobs":
                        Options["report-blobs"] = true;
                        break;

                    case "--minimum-hex-length":
                        int.TryParse(args[++i], out int hex);
                        hex = (hex < 1) ? 1 : hex;
                        hex = hex * 2;
                        HEX_REGEX = new Regex(HEX_REGEX_STRING.Replace("B", hex.ToString()).Replace("C", (hex - 1).ToString()), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));
                        break;

                    case "--minimum-base64-length":
                        int.TryParse(args[++i], out int base64);
                        base64 = (base64 < 1) ? 1 : base64;
                        base64 = base64 * 4;
                        BASE64_REGEX = new Regex(BASE64_REGEX_STRING.Replace("B", base64.ToString()), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));
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
  package-url                   package url to analyze (required, multiple allowed), or directory.

{GetCommonSupportedHelpText()}

optional arguments:
  --download-directory          the directory to download the package to
  --report-blobs                if set, blobs which cannot be determined to be strings, archives or binaries will be reported on (noisy)
  --minimum-hex-length          if set, overrides the default hex string detection length (default 8 pairs)
  --minimum-base64-length       if set, overrides the default base64 minimum string length (default 1 quad)
  --save-found-binaries-to      if set, encoded binaries which were found will be saved to this directory
  --save-archives-to            if set, encoded compressed files will be saved to this directory
  --save-blobs-to               if set, encoded blobs of indeterminate type will be saved to this directory
  --use-cache                   do not download the package if it is already present in the destination directory
  --help                        show this help message and exit
  --version                     show version number

");
        }
    }
}