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
            var innerContent = new Span<byte>(fileEntry.Content.ToArray(), 8, (int)fileEntry.Content.Length - 8);
            var filenameLookup = new Dictionary<int, string>();
            while (true)
            {
                if (innerContent.Length < 60)  // The header for each file is 60 bytes
                {
                    break;
                }
                var entryHeader = innerContent.Slice(0, 60);
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
                        var filename = Encoding.ASCII.GetString(innerContent.Slice(0, 16)).Trim();  // filename is 16 bytes
                        // TODO: If either of these lookup tables exist they should be first and indicate we can parallelize the rest of the lookups using the symbol table.
                        if (filename.Equals('/'))
                        {
                            // TODO https://en.wikipedia.org/wiki/Ar_(Unix)#System_V_(or_GNU)_variant
                            // System V symbol lookup table
                            // N = 32 bit big endian integers (entries in table)
                            // then N 32 bit big endian integers representing prositions in archive
                            // then N \0 terminated strings "symbol name" (possibly filename)
                        }
                        else if (filename.Equals("/SYM64/"))
                        {
                            // TODO https://en.wikipedia.org/wiki/Ar_(Unix)#System_V_(or_GNU)_variant
                            // GNU lookup table (archives larger than 4GB)
                            // N = 64 bit big endian integers (entries in table)
                            // then N 64 bit big endian integers representing prositions in archive
                            // then N \0 terminated strings "symbol name" (possibly filename)
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
                        }
                        var entryContent = innerContent.Slice(60, size);
                        using var entryStream = new MemoryStream(entryContent.ToArray());
                        results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                    }
                    // Entries are padded on even byte boundaries
                    // https://docs.oracle.com/cd/E36784_01/html/E36873/ar.h-3head.html
                    size = size % 2 == 1 ? size + 1 : size;
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
    }
}
