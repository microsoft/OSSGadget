using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    /**
     * Very simple implementation of an .Deb format parser, needed for Debian .deb archives.
     * See: https://en.wikipedia.org/wiki/Deb_(file_format)#/media/File:Deb_File_Structure.svg
     */
    public static class GnuArFile
    {
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
                var size = int.Parse(Encoding.ASCII.GetString(entryHeader.Slice(48, 10))); // header size in bytes

                // Header with list of file names
                if (entryHeader[0] == '/' && entryHeader[1] == '/')
                {
                    var fileNameBytes = innerContent.Slice(60, size);
                    var name = new StringBuilder();
                    var index = 0;
                    for (int i = 0; i< fileNameBytes.Length; i++)
                    {
                        if (fileNameBytes[i] == '/')
                        {
                            filenameLookup.Add(index, name.ToString());
                            name.Clear();
                            index = i + 1;
                        }
                        else
                        {
                            name.Append(fileNameBytes[i]);
                        }
                    }
                }
                else
                {
                    var filename = Encoding.ASCII.GetString(innerContent.Slice(0, 16)).Trim();  // filename is 16 bytes
                    if (filename.StartsWith('/'))
                    {
                        if (int.TryParse(filename, out int index))
                        {
                            try
                            {
                                filename = filenameLookup[index];
                            }
                            catch (Exception) { }
                        }
                    }
                    var entryContent = innerContent.Slice(60, size);
                    using var entryStream = new MemoryStream(entryContent.ToArray());
                    results.Add(new FileEntry(filename, fileEntry.FullPath, entryStream));
                }
                innerContent = innerContent[(60 + size)..];
            }
            return results;
        }
    }
}
