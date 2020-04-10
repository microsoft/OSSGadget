using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    /**
     * Very simple implementation of an Ar format parser, needed for Debian .deb archives.
     * Reference: https://en.wikipedia.org/wiki/Ar_(Unix)
     */
    public static class ArArchiveFile
    {
        // Simple method which returns a the file entries. We can't make this a continuation because
        // we're using spans.
        public static IEnumerable<FileEntry> GetFileEntries(FileEntry fileEntry)
        {
            if (fileEntry == null)
            {
                return Array.Empty<FileEntry>();
            }

            // First, cut out the global file header (8 bytes)
            var innerContent = new Span<byte>(fileEntry.Content.ToArray(), 8, (int)fileEntry.Content.Length - 8);
            var results = new List<FileEntry>();

            while (true)
            {
                if (innerContent.Length < 60)  // The header is 60 bytes
                {
                    break;
                }
                var entryHeader = innerContent.Slice(0, 60);
                var filename = Encoding.ASCII.GetString(innerContent.Slice(0, 16)).Trim();  // filename is 16 bytes
                var fileSizeBytes = entryHeader.Slice(48, 10); // File size is decimal-encoded, 10 bytes long
                var fileSize = int.Parse(Encoding.ASCII.GetString(fileSizeBytes.ToArray()).Trim());
                var entryContent = innerContent.Slice(60, fileSize);
                results.Add(new FileEntry(filename, fileEntry.FullPath, new MemoryStream(entryContent.ToArray())));
                innerContent = innerContent[(60 + fileSize)..];
            }
            return results;
        }
    }
}
