// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Archives.Rar;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;

namespace Microsoft.CST.OpenSource.Shared
{
    public static class Extractor
    {
        private const int BUFFER_SIZE = 4096;

        public static IEnumerable<FileEntry> ExtractFile(string filename)
        {
            using var memoryStream = new MemoryStream(File.ReadAllBytes(filename));
            return ExtractFile(new FileEntry(filename, "", memoryStream));
        }
        public static IEnumerable<FileEntry> ExtractFile(string filename, byte[] archiveBytes)
        {
            using var memoryStream = new MemoryStream(archiveBytes);
            return ExtractFile(new FileEntry(filename, "", memoryStream));
        }

        private static IEnumerable<FileEntry> ExtractFile(FileEntry fileEntry)
        {
            return (MiniMagic.DetectFileType(fileEntry)) switch
            {
                ArchiveFileType.ZIP => ExtractZipFile(fileEntry),
                ArchiveFileType.GZIP => ExtractGZipFile(fileEntry),
                ArchiveFileType.TAR => ExtractTarFile(fileEntry),
                ArchiveFileType.XZ => ExtractXZFile(fileEntry),
                ArchiveFileType.BZIP2 => ExtractBZip2File(fileEntry),
                ArchiveFileType.RAR => ExtractRarFile(fileEntry),
                ArchiveFileType.P7ZIP => Extract7ZipFile(fileEntry),
                _ => new[] { fileEntry },
            };
        }


        private static IEnumerable<FileEntry> ExtractZipFile(FileEntry fileEntry)
        {
            //Console.WriteLine("Extracting from Zip");
            //Console.WriteLine("Content Size => {0}", fileEntry.Content.Length);
            using var zipFile = new ZipFile(fileEntry.Content);
            foreach (ZipEntry zipEntry in zipFile)
            {
                //Console.WriteLine("Found {0}", zipEntry.Name);
                if (zipEntry.IsDirectory ||
                    zipEntry.IsCrypted ||
                    !zipEntry.CanDecompress)
                {
                    continue;
                }

                using var memoryStream = new MemoryStream();
                byte[] buffer = new byte[BUFFER_SIZE];
                var zipStream = zipFile.GetInputStream(zipEntry);
                StreamUtils.Copy(zipStream, memoryStream, buffer);

                var newFileEntry = new FileEntry(zipEntry.Name, fileEntry.FullPath, memoryStream);
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }
        private static IEnumerable<FileEntry> ExtractGZipFile(FileEntry fileEntry)
        {
            using var gzipStream = new GZipInputStream(fileEntry.Content);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);

            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
            if (fileEntry.Name.EndsWith(".tgz", System.StringComparison.CurrentCultureIgnoreCase))
            {
                newFilename = newFilename[0..^4] + ".tar";
            }

            var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, memoryStream);
            foreach (var extractedFile in ExtractFile(newFileEntry))
            {
                yield return extractedFile;
            }
        }

        private static IEnumerable<FileEntry> ExtractTarFile(FileEntry fileEntry)
        {

            TarEntry tarEntry;
            using var tarStream = new TarInputStream(fileEntry.Content);
            while ((tarEntry = tarStream.GetNextEntry()) != null)
            {
                if (tarEntry.IsDirectory)
                {
                    continue;
                }
                using var memoryStream = new MemoryStream();
                tarStream.CopyEntryContents(memoryStream);

                var newFileEntry = new FileEntry(tarEntry.Name, fileEntry.FullPath, memoryStream);
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }

        private static IEnumerable<FileEntry> ExtractXZFile(FileEntry fileEntry)
        {
            using var xzStream = new XZStream(fileEntry.Content);
            using var memoryStream = new MemoryStream();
            xzStream.CopyTo(memoryStream);

            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
            var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, memoryStream);
            foreach (var extractedFile in ExtractFile(newFileEntry))
            {
                yield return extractedFile;
            }
        }

        private static IEnumerable<FileEntry> ExtractBZip2File(FileEntry fileEntry)
        {
            using var bzip2Stream = new BZip2Stream(fileEntry.Content, SharpCompress.Compressors.CompressionMode.Decompress, false);
            using var memoryStream = new MemoryStream();
            bzip2Stream.CopyTo(memoryStream);

            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
            var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, memoryStream);
            foreach (var extractedFile in ExtractFile(newFileEntry))
            {
                yield return extractedFile;
            }
        }

        private static IEnumerable<FileEntry> ExtractRarFile(FileEntry fileEntry)
        {

            using var rarArchive = RarArchive.Open(fileEntry.Content);
            using var memoryStream = new MemoryStream();
            foreach (var entry in rarArchive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                var newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, entry.OpenEntryStream());
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }
        private static IEnumerable<FileEntry> Extract7ZipFile(FileEntry fileEntry)
        {
            using var rarArchive = RarArchive.Open(fileEntry.Content);
            using var memoryStream = new MemoryStream();
            foreach (var entry in rarArchive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                var newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, entry.OpenEntryStream());
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }
    }

}
