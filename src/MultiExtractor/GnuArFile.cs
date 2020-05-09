// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CST.OpenSource.MultiExtractor
{
    /**
     * Very simple implementation of an .Deb format parser, needed for Debian .deb archives.
     * See: https://en.wikipedia.org/wiki/Deb_(file_format)#/media/File:Deb_File_Structure.svg
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
            // TODO: This only supports < 4GB files because the span cannot be longer than int
            if (fileEntry.Content.Length > int.MaxValue)
            {
                Logger.Debug("Archive is larger than 4GB. If archive contains a 64bit lookup table it will be parsed in full, otherwise only the first ~4gb will be parsed.{0}:{1} is {2} bytes", fileEntry.FullPath, fileEntry.Name, fileEntry.Content.Length);
            }
            long position = 0;
            var innerContent = new Span<byte>(fileEntry.Content.ToArray(), 8, (int)fileEntry.Content.Length - 8);
            position += 8;
            var filenameLookup = new Dictionary<int, string>();
            while (true)
            {
                if (innerContent.Length < 60)  // The header for each file is 60 bytes
                {
                    break;
                }
                var entryHeader = innerContent.Slice(0, 60);
                position += 60;
                if (int.TryParse(Encoding.ASCII.GetString(entryHeader.Slice(48, 10)), out int size))// header size in bytes
                {
                    // Header with list of file names
                    if (entryHeader[0] == '/' && entryHeader[1] == '/')
                    {
                        var fileNameBytes = innerContent.Slice(60, size);
                        var name = new StringBuilder();
                        var index = 0;
                        for (int i = 0; i < fileNameBytes.Length; i++)
                        {
                            if (fileNameBytes[i] == '/')
                            {
                                filenameLookup.Add(index, name.ToString());
                                name.Clear();
                            }
                            else if (fileNameBytes[i] == '\n')
                            {
                                // The next filename would start on the next line
                                index = i + 1;
                            }
                            else
                            {
                                name.Append((char)fileNameBytes[i]);
                            }
                        }
                    }
                    else
                    {
                        var entryContent = innerContent.Slice(60, size);
                        var filename = Encoding.ASCII.GetString(innerContent.Slice(0, 16)).Trim();  // filename is 16 bytes

                        // TODO: If either of these lookup tables exist they should be first and indicate we can parallelize the rest of the lookups using the symbol table.
                        if (filename.Equals('/'))
                        {
                            // System V symbol lookup table
                            // N = 32 bit big endian integers (entries in table)
                            // then N 32 bit big endian integers representing prositions in archive
                            // then N \0 terminated strings "symbol name" (possibly filename)

                            var numEntries = IntFromBigEndianBytes(entryContent.Slice(0, 4).ToArray());
                            var filePositions = new int[numEntries];
                            for (int i = 0; i < numEntries; i++)
                            {
                                filePositions[i] = IntFromBigEndianBytes(entryContent.Slice((i + 1) * 4, 4).ToArray());
                            }
                            var innerPosition = numEntries * 4;
                            var index = 0;
                            var sb = new StringBuilder();
                            var fileEntries = new List<(int, string)>();
                            while (position < entryContent.Length)
                            {
                                if (entryContent.Slice(innerPosition, 1).ToArray()[0] == '\0')
                                {
                                    fileEntries.Add((filePositions[index++], sb.ToString()));
                                    sb.Clear();
                                }
                                else
                                {
                                    sb.Append(entryContent.Slice(innerPosition, 1).ToArray()[0]);
                                }
                            }
                            var fullData = new Span<byte>(fileEntry.Content.ToArray(), 0, (int)fileEntry.Content.Length);
                            foreach (var entry in fileEntries)
                            {
                                var entryData = fullData.Slice(entry.Item1, 60);
                                var innerEntryHeader = entryData.Slice(0, 60);
                                size += 60;
                                entryContent = entryData.Slice(60);
                                if (int.TryParse(Encoding.ASCII.GetString(innerEntryHeader.Slice(48, 10)), out int innerSize))// header size in bytes
                                {
                                    size += innerSize;
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

                                        using var entryStream = new MemoryStream(entryContent.ToArray());
                                        results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                                    }
                                    else
                                    {
                                        filename = entry.Item2;
                                        using var entryStream = new MemoryStream(entryContent.ToArray());
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

                            // This needs some other implementation as spans can only be int long

                            fileEntry.Content.Position = position;
                            var buffer = new byte[8];
                            fileEntry.Content.Read(buffer, 0, 8);
                            position += 8;
                            var numEntries = Int64FromBigEndianBytes(buffer);
                            var filePositions = new long[numEntries];
                            for (int i = 0; i < numEntries; i++)
                            {
                                fileEntry.Content.Read(buffer, 0, 8);
                                position += 8;
                                filePositions[i] = Int64FromBigEndianBytes(buffer);
                            }
                            var innerPosition = numEntries * 4;
                            var index = 0;
                            var sb = new StringBuilder();
                            var fileEntries = new List<(long, string)>();

                            while (position < entryContent.Length)
                            {
                                fileEntry.Content.Read(buffer, 0, 1);
                                position++;
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

                            foreach (var entry in fileEntries)
                            {
                                fileEntry.Content.Position = entry.Item1;
                                var headerBytes = new byte[60];
                                fileEntry.Content.Read(headerBytes, 0, 60);
                                var entryData = new Span<byte>(headerBytes);
                                size += 60;
                                if (int.TryParse(Encoding.ASCII.GetString(entryData.Slice(48, 10)), out int innerSize))// header size in bytes
                                {
                                    size += innerSize;
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
                                        filename = entry.Item2;
                                    }
                                    var bytes = new byte[innerSize];
                                    fileEntry.Content.Position = entry.Item1;
                                    fileEntry.Content.Read(bytes, 60, innerSize);
                                    using var entryStream = new MemoryStream(bytes);
                                    results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                                }
                            }
                        }
                        else if (filename.StartsWith('/'))
                        {
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

                            using var entryStream = new MemoryStream(entryContent.ToArray());
                            results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                        }
                        else
                        {
                            using var entryStream = new MemoryStream(entryContent.ToArray());
                            results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                        }
                    }
                    // Entries are padded on even byte boundaries
                    // https://docs.oracle.com/cd/E36784_01/html/E36873/ar.h-3head.html
                    size = size % 2 == 1 ? size + 1 : size;
                    position += size;
                    innerContent = innerContent[(60 + size)..];
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
