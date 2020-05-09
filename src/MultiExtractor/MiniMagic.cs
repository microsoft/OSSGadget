// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CST.OpenSource.MultiExtractor
{
    /// <summary>
    /// ArchiveTypes are the kinds of archive files that this module can process.
    /// </summary>
    public enum ArchiveFileType
    {
        UNKNOWN,
        ZIP,
        TAR,
        XZ,
        GZIP,
        BZIP2,
        RAR,
        P7ZIP,
        DEB,
        GNU_AR,
    }

    /// <summary>
    /// MiniMagic is a tiny implementation of a file type identifier based on binary signatures.
    /// </summary>
    public static class MiniMagic
    {
        /// <summary>
        /// Fallback using file extensions in case the binary signature doesn't match.
        /// </summary>
        private static readonly Dictionary<string, ArchiveFileType> FileExtensionMap = new Dictionary<string, ArchiveFileType>()
        {
            {"zip", ArchiveFileType.ZIP },
            {"apk", ArchiveFileType.ZIP },
            {"ipa", ArchiveFileType.ZIP },
            {"jar", ArchiveFileType.ZIP },
            {"ear", ArchiveFileType.ZIP },
            {"war", ArchiveFileType.ZIP },

            {"gz", ArchiveFileType.GZIP },
            {"tgz", ArchiveFileType.GZIP },

            {"tar", ArchiveFileType.TAR },
            {"gem", ArchiveFileType.TAR },

            {"xz", ArchiveFileType.XZ },

            {"bz2", ArchiveFileType.BZIP2 },

            {"rar", ArchiveFileType.RAR },

            {"7z", ArchiveFileType.P7ZIP }
        };

        public static ArchiveFileType DetectFileType(string filename)
        {
            #pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
            using var stream = new MemoryStream(File.ReadAllBytes(filename));
            #pragma warning restore SEC0116 // Path Tampering Unvalidated File Path
            var fileEntry = new FileEntry(filename, "", stream);
            return DetectFileType(fileEntry);
        }

        /// <summary>
        /// Detects the type of a file.
        /// </summary>
        /// <param name="fileEntry">FileEntry containing the file data.</param>
        /// <returns></returns>
        public static ArchiveFileType DetectFileType(FileEntry fileEntry)
        {
            if (fileEntry == null)
            {
                return ArchiveFileType.UNKNOWN;
            }

            var buffer = new byte[8];
            if (fileEntry.Content.Length >= 8)
            {
                fileEntry.Content.Position = 0;
                fileEntry.Content.Read(buffer, 0, 8);
                fileEntry.Content.Position = 0;
                if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
                {
                    return ArchiveFileType.ZIP;
                }

                if (buffer[0] == 0x1F && buffer[1] == 0x8B)
                {
                    return ArchiveFileType.GZIP;
                }

                if (buffer[0] == 0xFD && buffer[1] == 0x37 && buffer[2] == 0x7A && buffer[3] == 0x58 && buffer[4] == 0x5A && buffer[5] == 0x00)
                {
                    return ArchiveFileType.XZ;
                }
                if (buffer[0] == 0x42 && buffer[1] == 0x5A && buffer[2] == 0x68)
                {
                    return ArchiveFileType.BZIP2;
                }
                if ((buffer[0] == 0x52 && buffer[1] == 0x61 && buffer[2] == 0x72 && buffer[3] == 0x21 && buffer[4] == 0x1A && buffer[5] == 0x07 && buffer[6] == 0x00) ||
                    (buffer[0] == 0x52 && buffer[1] == 0x61 && buffer[2] == 0x72 && buffer[3] == 0x21 && buffer[4] == 0x1A && buffer[5] == 0x07 && buffer[6] == 0x01 && buffer[7] == 0x00))
                {
                    return ArchiveFileType.RAR;
                }
                if (buffer[0] == 0x37 && buffer[1] == 0x7A && buffer[2] == 0xBC && buffer[3] == 0xAF && buffer[4] == 0x27 && buffer[5] == 0x1C)
                {
                    return ArchiveFileType.P7ZIP;
                }
                // some kind of .ar https://en.wikipedia.org/wiki/Ar_(Unix)#BSD_variant
                if (buffer[0] == 0x21 && buffer[1] == 0x3c && buffer[2] == 0x61 && buffer[3] == 0x72 && buffer[4] == 0x63 && buffer[5] == 0x68 && buffer[6] == 0x3e)
                {
                    // .deb -https://manpages.debian.org/unstable/dpkg-dev/deb.5.en.html
                    fileEntry.Content.Position = 68;
                    fileEntry.Content.Read(buffer, 0, 4);
                    fileEntry.Content.Position = 0;
                    var encoding = new ASCIIEncoding();
                    if (encoding.GetString(buffer,0,4) == "2.0\n")
                    {
                        return ArchiveFileType.DEB;
                    }
                    // Some other kind of .ar
                    else
                    {
                        // Windows
                        if (buffer[7] == '\r')
                        {
                            // TODO: Support Windows /r/n formatted .ars
                            return ArchiveFileType.UNKNOWN;
                            fileEntry.Content.Position = 9;
                        }
                        else
                        {
                            fileEntry.Content.Position = 8;
                        }
                        byte[] headerBuffer = new byte[60];
                        fileEntry.Content.Read(headerBuffer, 0, 60);
                        fileEntry.Content.Position = 0;
                        var size = int.Parse(Encoding.ASCII.GetString(headerBuffer.AsSpan().Slice(48, 10))); // header size in bytes
                        if (size > 0)
                        {
                            if (headerBuffer[58]=='`')
                            {
                                return ArchiveFileType.GNU_AR;
                            }
                        }
                    }
                }
            }

            if (fileEntry.Content.Length >= 262)
            {
                fileEntry.Content.Position = 257;
                fileEntry.Content.Read(buffer, 0, 5);
                fileEntry.Content.Position = 0;
                if (buffer[0] == 0x75 && buffer[1] == 0x73 && buffer[2] == 0x74 && buffer[3] == 0x61 && buffer[4] == 0x72)
                {
                    return ArchiveFileType.TAR;
                }
            }

            // Fall back to file extensions
            #pragma warning disable CA1308 // Normalize strings to uppercase
            string fileExtension = Path.GetExtension(fileEntry.Name.ToLowerInvariant());
            #pragma warning restore CA1308 // Normalize strings to uppercase

            if (fileExtension.StartsWith('.'))
            {
                fileExtension = fileExtension.Substring(1);
            }
            if (!MiniMagic.FileExtensionMap.TryGetValue(fileExtension, out ArchiveFileType fileType))
            {
                fileType = ArchiveFileType.UNKNOWN;
            }
            return fileType;
        }
    }
}
