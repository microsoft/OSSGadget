// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NLog.Fluent;
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
            var content = new Span<byte>(fileEntry.Content.ToArray());
            (int globalHeaderSize, int localHeaderSize) = (8, 60);
            if (content[7] == '\r')
            {
                Logger.Debug("Couldn't parse Windows formatted .ar file.");
                return results;
                // Windows format
                (globalHeaderSize, localHeaderSize) = (9, 61);
            }
            var innerContent = content.Slice(globalHeaderSize);
            var filenameLookup = new Dictionary<int, string>();
            while (true)
            {
                if (innerContent.Length < localHeaderSize)  // The header for each file is 60 bytes
                {
                    break;
                }

                var entryHeader = innerContent.Slice(0, localHeaderSize);
                var versionString = Encoding.ASCII.GetString(entryHeader.Slice(48, 10)).Trim();
                if (int.TryParse(versionString, out int size))// header size in bytes
                {
                    // Header with list of file names
                    if (entryHeader[0] == '/' && entryHeader[1] == '/')
                    {
                        var fileNameBytes = innerContent.Slice(localHeaderSize, size);
                        var name = new StringBuilder();
                        var index = 0;
                        var currentByte = 0;
                        var initialLength = fileNameBytes.Length;
                        while (currentByte < size)
                        {
                            if (fileNameBytes[currentByte] == '/')
                            {
                                filenameLookup.Add(index, name.ToString());
                                name.Clear();
                            }
                            else if (fileNameBytes[currentByte] == '\r')
                            {
                                // GNU Ar on windows adds /r/n and doesn't count the /r.
                                // Everytime we encounter an \r we have to bump up the size by one to account for it
                                fileNameBytes = innerContent.Slice(localHeaderSize, ++size);
                            }
                            else if (fileNameBytes[currentByte] == '\n')
                            {
                                // Update the index for looking up the tags later
                                // We Adjust the index by the number of \r we've seen (size - initialLength).  
                                // These are present in Windows generated AR file but are not counted in the size specified in the header
                                index = currentByte + 1 - (size - initialLength);
                            }
                            else
                            {
                                name.Append((char)fileNameBytes[currentByte]);
                            }
                            currentByte++;
                        }
                    }
                    else
                    {
                        var filename = Encoding.ASCII.GetString(innerContent.Slice(0, 16)).TrimEnd();  // filename is 16 bytes
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
                        // Reference names start with a / and refer to the filename table
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
                        // Literal names end with a /, which we don't want
                        else if (filename.EndsWith('/'))
                        {
                            filename = filename[0..^1];
                        }

                        var entryContent = innerContent.Slice(localHeaderSize, size);
                        using var entryStream = new MemoryStream(entryContent.ToArray());
                        results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                    }
                    // Data ends on even blocks
                    size = size % 2 == 1 ? size + 1 : size;
                    innerContent = innerContent[(localHeaderSize + size)..];
                }
                else
                {
                    // Not a valid header, we couldn't parse the file size, return what we have.
                    return results;
                }
            }
            return results;
        }
    }
}
