// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CST.OpenSource.MultiExtractor
{
    /**
     * Gnu Ar file parser.  Supports SystemV style lookup tables in both 32 and 64 bit mode. 
     * Maximum individual file size is 2GB per .NET Core restriction.
     * TODO: Make byte arrays multi dimensional to allow for larger files.  
     * The limitation a single dimension can't exceed ~2 billion
     */
    public static class GnuArFile
    {
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        // Simple method which returns a the file entries. We can't make this a continuation because
        // we're using spans.
        public static IEnumerable<FileEntry> GetFileEntries(FileEntry fileEntry)
        {
            var results = new List<FileEntry>();

            if (fileEntry == null)
            {
                return results;
            }
            // First, cut out the file signature (8 bytes)
            fileEntry.Content.Position = 8;
            var filenameLookup = new Dictionary<int, string>();
            var headerBuffer = new Span<byte>(new byte[60]);
            while (true)
            {
                if (fileEntry.Content.Length - fileEntry.Content.Position < 60)  // The header for each file is 60 bytes
                {
                    break;
                }

                fileEntry.Content.Read(headerBuffer);
                fileEntry.Content.Position += 60;
                if (int.TryParse(Encoding.ASCII.GetString(headerBuffer.Slice(48, 10)), out int size))// header size in bytes
                {
                    // Header with list of file names
                    if (headerBuffer[0] == '/' && headerBuffer[1] == '/')
                    {
                        var fileNamesBytes = new Span<byte>(new byte[size]);
                        fileEntry.Content.Read(fileNamesBytes);
                        fileEntry.Content.Position += size;
                        var name = new StringBuilder();
                        var index = 0;
                        for (int i = 0; i < fileNamesBytes.Length; i++)
                        {
                            if (fileNamesBytes[i] == '/')
                            {
                                filenameLookup.Add(index, name.ToString());
                                name.Clear();
                            }
                            else if (fileNamesBytes[i] == '\n')
                            {
                                // The next filename would start on the next line
                                index = i + 1;
                            }
                            else
                            {
                                name.Append((char)fileNamesBytes[i]);
                            }
                        }
                    }
                    else
                    {
                        var fileNameBytes = new Span<byte>(new byte[16]);// filename is 16 bytes
                        fileEntry.Content.Read(fileNameBytes);
                        var filename = Encoding.ASCII.GetString(fileNameBytes).Trim();  

                        if (filename.Equals('/'))
                        {
                            // System V symbol lookup table
                            // N = 32 bit big endian integers (entries in table)
                            // then N 32 bit big endian integers representing prositions in archive
                            // then N \0 terminated strings "symbol name" (possibly filename)

                            var tableContents = new Span<byte>(new byte[size]);
                            fileEntry.Content.Read(tableContents);
                            fileEntry.Content.Position += size;

                            var numEntries = IntFromBigEndianBytes(tableContents.Slice(0, 4).ToArray());
                            var filePositions = new int[numEntries];
                            for (int i = 0; i < numEntries; i++)
                            {
                                filePositions[i] = IntFromBigEndianBytes(tableContents.Slice((i + 1) * 4, 4).ToArray());
                            }

                            var index = 0;
                            var sb = new StringBuilder();
                            var fileEntries = new List<(int, string)>();

                            for (int i = 0; i< tableContents.Length; i++)
                            {
                                if (tableContents.Slice(i, 1)[0] == '\0')
                                {
                                    fileEntries.Add((filePositions[index++], sb.ToString()));
                                    sb.Clear();
                                }
                                else
                                {
                                    sb.Append(tableContents.Slice(i, 1)[0]);
                                }
                            }

                            foreach (var entry in fileEntries)
                            {
                                fileEntry.Content.Position = entry.Item1;
                                fileEntry.Content.Read(headerBuffer);
                                fileEntry.Content.Position += 60;

                                if (int.TryParse(Encoding.ASCII.GetString(headerBuffer.Slice(48, 10)), out int innerSize))// header size in bytes
                                {
                                    var entryContent = new byte[innerSize];
                                    fileEntry.Content.Read(entryContent);
                                    fileEntry.Content.Position += innerSize;

                                    if (filename.StartsWith('/'))
                                    {
                                        if (int.TryParse(filename[1..], out int innerIndex))
                                        {
                                            try
                                            {
                                                filename = filenameLookup[innerIndex];
                                            }
                                            catch (Exception)
                                            {
                                                Logger.Debug("Expected to find a filename at index {0}", innerIndex);
                                            }
                                        }

                                        using var entryStream = new MemoryStream(entryContent);
                                        results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                                    }
                                    else
                                    {
                                        filename = entry.Item2;
                                        using var entryStream = new MemoryStream(entryContent);
                                        results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                                    }
                                }
                            }
                        }
                        else if (filename.Equals("/SYM64/"))
                        {
                            // TODO https://en.wikipedia.org/wiki/Ar_(Unix)#System_V_(or_GNU)_variant
                            // GNU lookup table (archives larger than 4GB)
                            // N = 64 bit big endian integers (entries in table)
                            // then N 64 bit big endian integers representing positions in archive
                            // then N \0 terminated strings "symbol name" (possibly filename)

                            var buffer = new byte[8];
                            fileEntry.Content.Read(buffer, 0, 8);
                            fileEntry.Content.Position += 8;

                            var numEntries = Int64FromBigEndianBytes(buffer);
                            var filePositions = new long[numEntries];

                            for (int i = 0; i < numEntries; i++)
                            {
                                fileEntry.Content.Read(buffer, 0, 8);
                                fileEntry.Content.Position += 8;
                                filePositions[i] = Int64FromBigEndianBytes(buffer);
                            }

                            var index = 0;
                            var sb = new StringBuilder();
                            var fileEntries = new List<(long, string)>();

                            while (fileEntry.Content.Position < size)
                            {
                                fileEntry.Content.Read(buffer, 0, 1);
                                fileEntry.Content.Position += 1;
                                if (buffer[0] == '\0')
                                {
                                    fileEntries.Add((filePositions[index++], sb.ToString()));
                                    sb.Clear();
                                }
                                else
                                {
                                    sb.Append(buffer[0]);
                                }
                            }

                            foreach (var innerEntry in fileEntries)
                            {
                                fileEntry.Content.Position = innerEntry.Item1;

                                fileEntry.Content.Read(headerBuffer);
                                fileEntry.Content.Position += 60;

                                if (int.TryParse(Encoding.ASCII.GetString(headerBuffer.Slice(48, 10)), out int innerSize))// header size in bytes
                                {
                                    if (filename.StartsWith('/'))
                                    {
                                        if (int.TryParse(filename[1..], out int innerIndex))
                                        {
                                            try
                                            {
                                                filename = filenameLookup[innerIndex];
                                            }
                                            catch (Exception)
                                            {
                                                Logger.Debug("Expected to find a filename at index {0}", innerIndex);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        filename = innerEntry.Item2;
                                    }
                                    var bytes = new byte[innerSize];
                                    fileEntry.Content.Read(bytes, 0, innerSize);
                                    using var entryStream = new MemoryStream(bytes);
                                    results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                                }
                            }
                        }
                        else if (filename.StartsWith('/'))
                        {
                            fileEntry.Content.Read(headerBuffer);
                            fileEntry.Content.Position += 60;

                            if (int.TryParse(filename[1..], out int index))
                            {
                                try
                                {
                                    filename = filenameLookup[index];
                                }
                                catch (Exception)
                                {
                                    Logger.Debug("Expected to find a filename at index {0}", index);
                                }
                            }

                            var bytes = new byte[size];
                            fileEntry.Content.Read(bytes, 0, size);
                            fileEntry.Content.Position += size;

                            using var entryStream = new MemoryStream(bytes);
                            results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                        }
                        else
                        {
                            var bytes = new byte[size];
                            fileEntry.Content.Read(bytes, 0, size);
                            fileEntry.Content.Position += size;

                            using var entryStream = new MemoryStream(bytes);
                            results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                        }
                    }
                    // Entries are padded on even byte boundaries
                    // https://docs.oracle.com/cd/E36784_01/html/E36873/ar.h-3head.html
                    fileEntry.Content.Position = fileEntry.Content.Position % 2 == 1 ? fileEntry.Content.Position + 1 : fileEntry.Content.Position;
                }
                else
                {
                    // Not a valid header, we couldn't parse the file size.
                    return results;
                }
            }
            return results;
        }

        public static int IntFromBigEndianBytes(byte[] value)
        {
            if (value.Length == 4)
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }
                return BitConverter.ToInt32(value);
            }
            return -1;
        }

        public static long Int64FromBigEndianBytes(byte[] value)
        {
            if (value.Length == 8)
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }
                return BitConverter.ToInt64(value);
            }
            return -1;
        }
    }
}
