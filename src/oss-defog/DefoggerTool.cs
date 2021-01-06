// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
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
    internal class DefoggerTool : OSSGadget
    {
        /// <summary>
        ///     Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-defogger";

        /// <summary>
        ///     Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(DefoggerTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>
        ///     Regular expression that matches Base64-encoded text.
        /// </summary>
        private static readonly Regex BASE64_REGEX = new Regex("(([A-Z0-9+\\/]{4})+([A-Z0-9+\\/]{3}=|[A-Z0-9+\\/]{2}==)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        ///     Regular expression that matches hex-encoded text.
        /// </summary>
        private static readonly Regex HEX_REGEX = new Regex(@"(0x)?([A-F0-9]{16,})", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        ///     Short strings must match this regular expression to be reported.
        /// </summary>
        private static readonly Regex SHORT_INTERESTING_STRINGS_REGEX = new Regex(@"^[A-Z0-9\-:]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        ///     Do not analyze binary files (those with a MIME type that matches this regular expression).
        /// </summary>
        private static readonly Regex IGNORE_MIME_REGEX = new Regex(@"audio|video|x-msdownload", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        ///     Only report detected strings this length or longer.
        /// </summary>
        private const int DEFAULT_MINIMUM_STRING_LENGTH = 8;

        /// <summary>
        ///     Strings longer than this are interesting.
        /// </summary>
        private const int INTERESTING_STRINGS_CUTOFF = 24;

        /// <summary>
        ///     Enum of executable types
        /// </summary>
        public enum EXECUTABLE_TYPE
        {
            UNKNOWN,
            WINDOWS,
            MACOS,
            LINUX,
            NONE,
            JAVA
        }

        public static byte[] HexStringToBytes(string hex)
        {
            try
            {
                if (hex is null) { throw new ArgumentNullException(nameof(hex)); }

                var ascii = new byte[hex.Length / 2];

                for (int i = 0; i < hex.Length; i += 2)
                {
                    var hs = hex.Substring(i, 2);
                    uint decval = System.Convert.ToUInt32(hs, 16);
                    char character = System.Convert.ToChar(decval);
                    ascii[i / 2] = (byte)character;
                }

                return ascii;
            }
            catch (Exception e) when (
                e is ArgumentException
                || e is OverflowException
                || e is NullReferenceException)
            {
                Logger.Debug("Couldn't convert hex string {0} to ascii", hex);
            }
            return Array.Empty<byte>();
        }

        /// <summary>
        ///     Linux Elf
        /// </summary>
        public static readonly byte[] ElfMagicNumber = HexStringToBytes("7F454C46");

        /// <summary>
        ///     Java Classes
        /// </summary>
        public static readonly byte[] JavaMagicNumber = HexStringToBytes("CAFEBEBE");

        /// <summary>
        ///     Mac Binary Magic numbers
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
            HexStringToBytes("CAFEBEBE")
        };

        /// <summary>
        ///     Windows Binary header
        /// </summary>
        public static readonly byte[] WindowsMagicNumber = HexStringToBytes("4D5A");

        /// <summary>
        ///     Gets the executable type of the given stream.
        /// </summary>
        /// <param name="input">Stream bytes to check</param>
        /// <returns>The executable type</returns>
        public static EXECUTABLE_TYPE GetExecutableType(Stream input)
        {
            if (input == null) { return EXECUTABLE_TYPE.UNKNOWN; }
            if (input.Length < 4) { return EXECUTABLE_TYPE.NONE; }

            var fourBytes = new byte[4];
            var initialPosition = input.Position;

            try
            {
                input.Read(fourBytes);
                input.Position = initialPosition;
            }
            catch (Exception e)
            {
                Logger.Debug("Couldn't chomp 4 bytes ({1}:{2})", e.GetType().ToString(), e.Message);
                return EXECUTABLE_TYPE.UNKNOWN;
            }

            switch (fourBytes)
            {
                case var span when span.SequenceEqual(ElfMagicNumber):
                    return EXECUTABLE_TYPE.LINUX;

                case var span when span.SequenceEqual(JavaMagicNumber):
                    return EXECUTABLE_TYPE.JAVA;

                case var span when MacMagicNumbers.Contains(span):
                    return EXECUTABLE_TYPE.MACOS;

                case var span when span[0..2].SequenceEqual(WindowsMagicNumber):
                    return EXECUTABLE_TYPE.WINDOWS;

                default:
                    return EXECUTABLE_TYPE.NONE;
            }
        }

        /// <summary>
        ///     Command line options passed into this tool.
        /// </summary>
        private readonly IDictionary<string, object?> Options = new Dictionary<string, object?>()
        {
            { "target", new List<string>() },
            { "download-directory", null },
            { "use-cache", false },
            { "save-found-binaries-to", null },
            { "save-archives-to", null }
        };

        /// <summary>
        ///     Identified findings from the analysis.
        /// </summary>
        public IList<EncodedString> Findings { get; private set; }
        public List<EncodedBinary> BinaryFindings { get; }
        public List<EncodedArchive> ArchiveFindings { get; }


        /// <summary>
        ///     The specific type of encoding detected.
        /// </summary>
        public enum EncodedStringType
        {
            Base64,
            Hex,
            Compressed
        }

        /// <summary>
        ///     The encoded string and type.
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
        ///     The encoded archive and type.
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
        ///     The encoded binary and type.
        /// </summary>
        public class EncodedBinary
        {
            public EXECUTABLE_TYPE Type;
            public string Filename;
            public string EncodedText;
            public Stream DecodedBinary;

            public EncodedBinary(EXECUTABLE_TYPE Type, string Filename, string EncodedText, Stream DecodedBinary)
            {
                this.Type = Type;
                this.Filename = Filename;
                this.EncodedText = EncodedText;
                this.DecodedBinary = DecodedBinary;
            }
        }

        public static byte[] ReadToEnd(System.IO.Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }

        private static void Main(string[] args)
        {
            CommonInitialization.Initialize();

            Logger.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");

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
                            Logger.Info("{0}: {1} -> {2}", finding.Filename, finding.EncodedText, finding.DecodedText);
                        }

                        var binaryDir = (string?)defoggerTool.Options["save-found-binaries-to"];
                        var binaryNumber = 0;
                        foreach (var binaryFinding in defoggerTool.BinaryFindings)
                        {
                            Logger.Info("{0}: {1} -> {2}", binaryFinding.Filename, binaryFinding.EncodedText, binaryFinding.Type);
                            if (binaryDir is string)
                            {
                                var path = Path.Combine(binaryDir, binaryFinding.Filename, $"binary-{binaryNumber}");
                                Logger.Info("Saving to ", path);
                                File.WriteAllBytes(path,ReadToEnd(binaryFinding.DecodedBinary));
                                binaryNumber++;
                            }
                        }

                        var archiveDir = (string?)defoggerTool.Options["save-archives-to"];
                        var archiveNumber = 0;
                        foreach (var archiveFinding in defoggerTool.ArchiveFindings)
                        {
                            Logger.Info("{0}: {1} -> {2}", archiveFinding.Filename, archiveFinding.EncodedText, archiveFinding.Type);
                            if (archiveDir is string)
                            {
                                var path = Path.Combine(archiveDir, archiveFinding.Filename, $"archive-{archiveNumber++}");
                                Logger.Info("Saving to ", path);
                                File.WriteAllBytes(path, ReadToEnd(archiveFinding.DecodedArchive));
                                archiveNumber++;
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
        ///     Initializes a new DefoggerTool instance.
        /// </summary>
        public DefoggerTool() : base()
        {
            Findings = new List<EncodedString>();
            BinaryFindings = new List<EncodedBinary>();
        }

        /// <summary>
        ///     Analyze a package by downloading it first.
        /// </summary>
        /// <param name="purl"> The package-url of the package to analyze. </param>
        /// <returns> n/a </returns>
        public async Task AnalyzePackage(PackageURL purl, string? destinationDirectory, bool doCaching)
        {
            Logger.Trace("AnalyzePackage({0})", purl.ToString());

            var packageDownloader = new PackageDownloader(purl, destinationDirectory, doCaching);
            foreach (var directory in await packageDownloader.DownloadPackageLocalCopy(purl, false, true))
            {
                AnalyzeDirectory(directory);
            }

            packageDownloader.ClearPackageLocalCopyIfNoCaching();
        }

        /// <summary>
        ///     Analyzes a directory of files.
        /// </summary>
        /// <param name="directory"> directory to analyze. </param>
        public void AnalyzeDirectory(string directory)
        {
            Logger.Trace("AnalyzeDirectory({0})", directory);
            var fileList = Directory.EnumerateFiles(directory, @"*.*", SearchOption.AllDirectories);
            foreach (var filename in fileList)
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
        ///     Analyzes a single file.
        /// </summary>
        /// <param name="filename"> filename to analyze </param>
        public void AnalyzeFile(string filename)
        {
            Logger.Trace("AnalyzeFile({0})", filename);

            var mimeType = MimeTypeMap.GetMimeType(Path.GetExtension(filename));
            if (IGNORE_MIME_REGEX.IsMatch(mimeType))
            {
                Logger.Debug("Ignoring {0}; invalid MIME type: {1}", filename, mimeType);
                return;
            }

#pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
            var fileContents = File.ReadAllText(filename);
#pragma warning restore SEC0116 // Path Tampering Unvalidated File Path

            AnalyzeFile(filename, fileContents);
        }

        public void AnalyzeFile(string filename, string fileContents)
        {
            foreach (Match match in BASE64_REGEX.Matches(fileContents).Where(match => match != null))
            {
                if (!match.Success)
                {
                    continue;
                }

                // Try to decode and then re-encode. Are we successful, and do we get the same value we
                // started out with? This will filter out Base64-encoded binary data, which is what we want.
                try
                {
                    var bytes = Convert.FromBase64String(match.Value);
                    var decoded = Encoding.UTF8.GetString(bytes);
                    // Does the re-encoded string match?
                    var reencoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded));

                    if (match.Value.Equals(reencoded))
                    {
                        var entry = new FileEntry("bytes", new MemoryStream(bytes),new FileEntry(filename,new MemoryStream()));

                        var exeType = GetExecutableType(entry.Content);

                        if (exeType is not EXECUTABLE_TYPE.NONE && exeType is not EXECUTABLE_TYPE.UNKNOWN)
                        {
                            BinaryFindings.Add(new EncodedBinary(exeType, filename, match.Value, entry.Content));
                        }
                        else
                        {
                            var archiveFileType = MiniMagic.DetectFileType(entry);

                            var extractor = new Extractor();

                            if (archiveFileType is not ArchiveFileType.UNKNOWN && archiveFileType is not ArchiveFileType.INVALID)
                            {
                                ArchiveFindings.Add(new EncodedArchive(archiveFileType, filename, match.Value, entry.Content));
                                foreach (var extractedEntry in extractor.Extract(entry))
                                {
                                    exeType = GetExecutableType(extractedEntry.Content);
                                    if (exeType is not EXECUTABLE_TYPE.NONE && exeType is not EXECUTABLE_TYPE.UNKNOWN)
                                    {
                                        BinaryFindings.Add(new EncodedBinary(exeType, filename, match.Value, entry.Content));
                                    }
                                    else
                                    {
                                        var zippedString = new StreamReader(extractedEntry.Content).ReadToEnd();
                                        AnalyzeFile(extractedEntry.FullPath, zippedString);
                                    }
                                }
                            }
                            else
                            {
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

        /// <summary>
        ///     Converts hex data to a string
        /// </summary>
        /// <param name="hex">The Hex data to decode</param>
        /// <returns>The decoded string</returns>
        private static string HexToString(string hex)
        {
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
        ///     Decides whether the given string is interesting or not
        /// </summary>
        /// <param name="s"> string to check </param>
        /// <returns> </returns>
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
        ///     Parses options for this program.
        /// </summary>
        /// <param name="args"> arguments (passed in from the user) </param>
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

                    case "--save-found-binaries-to":
                        Options["save-found-binaries-to"] = args[++i];
                        break;

                    case "--save-archives-to":
                        Options["save-archives-to "] = args[++i];
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
        ///     Displays usage information for the program.
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
  --save-found-binaries-to      if set, encoded binaries which were found will be extracted to this directory
  --save-archives-to          if set, encoded compressed files will be extracted to this directory
  --use-cache                   do not download the package if it is already present in the destination directory
  --help                        show this help message and exit
  --version                     show version number
");
        }
    }
}