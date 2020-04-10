// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;

namespace Microsoft.CST.OpenSource.Shared
{
    public class Extractor
    {
        /// <summary>
        /// Internal buffer size for extraction
        /// </summary>
        private const int BUFFER_SIZE = 32768;

        /// <summary>
        /// By default, stop extracting if the total number of bytes
        /// seen is greater than this multiple of the original archive
        /// size. Used to avoid denial of service (zip bombs and the like).
        /// </summary>
        private const double DEFAULT_MAX_EXTRACTED_BYTES_RATIO = 60.0;
        
        /// <summary>
        /// By default, stop processing after this time span. Used to avoid
        /// denial of service (zip bombs and the like).
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(300);

        /// <summary>
        /// The maximum number of bytes to extract from the archive and
        /// all embedded archives. Set to 0 to remove limit. Note that
        /// MaxExpansionRatio may also apply. Defaults to 0.
        /// </summary>
        public long MaxExtractedBytes { get; set; } = 0;

        /// <summary>
        /// Backing store for MaxExtractedBytesRatio.
        /// </summary>
        private double _MaxExtractedBytesRatio;

        /// <summary>
        /// The maximum number of bytes to extract from the archive and
        /// all embedded archives, relative to the size of the initial
        /// archive. The default value of 100 means if the archive is 5k
        /// in size, stop processing after 500k bytes are extracted. Set
        /// this to 0 to mean, 'no limit'. Not that MaxExtractedBytes
        /// may also apply.
        /// </summary>
        public double MaxExtractedBytesRatio {
            get
            {
                return _MaxExtractedBytesRatio;
            }

            set
            {
                _MaxExtractedBytesRatio = Math.Max(value, 0);
            }
        }

        /// <summary>
        /// Logger for interesting events.
        /// </summary>
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Times extraction operations to avoid denial of service.
        /// </summary>
        private Stopwatch GovernorStopwatch;

        /// <summary>
        /// Stores the number of bytes left before we abort (denial of service).
        /// </summary>
        private long CurrentOperationProcessedBytesLeft = 0;

        public Extractor()
        {
            MaxExtractedBytesRatio = DEFAULT_MAX_EXTRACTED_BYTES_RATIO;
            GovernorStopwatch = new Stopwatch();
        }

        private void ResetResourceGovernor()
        {
            Logger.Trace("ResetResourceGovernor()");
            GovernorStopwatch.Reset();
            CurrentOperationProcessedBytesLeft = 0;
        }

        private void ResetResourceGovernor(Stream stream)
        {
            Logger.Trace("ResetResourceGovernor()");

            if (stream == null)
            {
                throw new ArgumentNullException("MemoryStream must not be null.");
            }

            GovernorStopwatch = Stopwatch.StartNew();

            // Default value is we take MaxExtractedBytes (meaning, ratio is not defined)
            CurrentOperationProcessedBytesLeft = MaxExtractedBytes;
            if (MaxExtractedBytesRatio > 0)
            {
                long streamLength;
                try
                {
                    streamLength = stream.Length;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Unable to get length of stream.");
                }

                // Ratio *is* defined, so the max value would be based on the stream length
                var maxViaRatio = (long)(MaxExtractedBytesRatio * streamLength);
                // Assign the samller of the two, accounting for MaxExtractedBytes == 0 means, 'no limit'.
                CurrentOperationProcessedBytesLeft = Math.Min(maxViaRatio, MaxExtractedBytes > 0 ? MaxExtractedBytes : long.MaxValue);
            }
        }

        /// <summary>
        /// Checks to ensure we haven't extracted too many bytes, or taken too long.
        /// This exists primarily to mitigate the risks of quines (archives that 
        /// contain themselves) and zip bombs (specially constructed to expand to huge
        /// sizes).
        /// Ref: https://alf.nu/ZipQuine
        /// </summary>
        /// <param name="additionalBytes"></param>
        private void CheckResourceGovernor(long additionalBytes = 0)
        {
            Logger.ConditionalTrace("CheckResourceGovernor(duration={0}, bytes={1})", GovernorStopwatch.Elapsed.TotalMilliseconds, CurrentOperationProcessedBytesLeft);

            if (GovernorStopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException(string.Format($"Processing timeout exceeded: {GovernorStopwatch.Elapsed.TotalMilliseconds} ms."));
            }

            if (CurrentOperationProcessedBytesLeft - additionalBytes <= 0)
            {
                throw new OverflowException(string.Format($"Too many bytes extracted, exceeding limit."));
            }
        }


        /// <summary>
        /// Extracts files from the file 'filename'.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        public IEnumerable<FileEntry> ExtractFile(string filename)
        {
            if (!File.Exists(filename))
            {
                Logger.Warn("ExtractFile called, but {0} does not exist.", filename);
                return Array.Empty<FileEntry>();
            }

            #pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
            using var memoryStream = new MemoryStream(File.ReadAllBytes(filename));
            #pragma warning restore SEC0116 // Path Tampering Unvalidated File Path

            ResetResourceGovernor(memoryStream);
            var result = ExtractFile(new FileEntry(filename, "", memoryStream));
            return result;
        }

        /// <summary>
        /// Extracts files from the file, identified by 'filename', but with 
        /// contents passed through 'archiveBytes'. Note that 'filename' does not
        /// have to exist; it will only be used to identify files extracted.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        public IEnumerable<FileEntry> ExtractFile(string filename, byte[] archiveBytes)
        {
            using var memoryStream = new MemoryStream(archiveBytes);
            ResetResourceGovernor(memoryStream);
            var result = ExtractFile(new FileEntry(filename, "", memoryStream));
            return result;
        }

        /// <summary>
        /// Extracts files from the given FileEntry, using the appropriate
        /// extractors, recursively.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractFile(FileEntry fileEntry)
        {
            Logger.Trace("ExtractFile({0})", fileEntry.FullPath);
            var rawFileUsed = false;

            CheckResourceGovernor();
            IEnumerable<FileEntry> result;
            
            try
            {
                var fileEntryType = MiniMagic.DetectFileType(fileEntry);
                switch(fileEntryType)
                {
                    case ArchiveFileType.ZIP:
                        result = ExtractZipFile(fileEntry);
                        break;
                    case ArchiveFileType.GZIP:
                        result = ExtractGZipFile(fileEntry);
                        break;
                    case ArchiveFileType.TAR:
                        result = ExtractTarFile(fileEntry);
                        break;
                    case ArchiveFileType.XZ:
                        result = ExtractXZFile(fileEntry);
                        break;
                    case ArchiveFileType.BZIP2:
                        result = ExtractBZip2File(fileEntry);
                        break;
                    case ArchiveFileType.RAR:
                        result = ExtractRarFile(fileEntry);
                        break;
                    case ArchiveFileType.P7ZIP:
                        result = Extract7ZipFile(fileEntry);
                        break;
                    case ArchiveFileType.DEB:
                        result = ExtractArFile(fileEntry);
                        break;
                    default:
                        rawFileUsed = true;
                        result = new[] { fileEntry };
                        break;
                }
            }
            catch(Exception ex)
            {
                Logger.Debug(ex, "Error extracting {0}: {1}", fileEntry.FullPath, ex.Message);
                rawFileUsed = true;
                result = new[] { fileEntry };   // Default is to not try to extract.
            }

            if (rawFileUsed)
            {
                // We only increment the procesed bytes for non-archives,
                // since archives we process are never actually written to disk.
                CurrentOperationProcessedBytesLeft -= fileEntry.Content.Length;
            }
            return result;
        }

        /// <summary>
        /// Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractZipFile(FileEntry fileEntry)
        {
            using var zipFile = new ZipFile(fileEntry.Content);
            foreach (ZipEntry zipEntry in zipFile)
            {
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

        /// <summary>
        /// Extracts an Gzip file contained in fileEntry.
        /// Since this function is recursive, even though Gzip only supports a single
        /// compressed file, that inner file could itself contain multiple others.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractGZipFile(FileEntry fileEntry)
        {
            using var gzipArchive = GZipArchive.Open(fileEntry.Content);
            foreach (var entry in gzipArchive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                CheckResourceGovernor(entry.Size);
                
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                if (fileEntry.Name.EndsWith(".tgz", StringComparison.InvariantCultureIgnoreCase))
                {
                    newFilename = newFilename[0..^4] + ".tar";
                }

                var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, entry.OpenEntryStream());
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }

        /// <summary>
        /// Extracts a tar file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractTarFile(FileEntry fileEntry)
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
                CheckResourceGovernor((long)tarStream.Length);
                tarStream.CopyEntryContents(memoryStream);

                var newFileEntry = new FileEntry(tarEntry.Name, fileEntry.FullPath, memoryStream);
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }

        /// <summary>
        /// Extracts an .XZ file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractXZFile(FileEntry fileEntry)
        {
            using var xzStream = new XZStream(fileEntry.Content);
            using var memoryStream = new MemoryStream();

            // SharpCompress does not expose metadata without a full read,
            // so we need to decompress first, and then abort if the bytes
            // exceeded the governor's capacity.
            xzStream.CopyTo(memoryStream);
            
            var streamLength = xzStream.Index.Records?.Select(r => r.UncompressedSize)
                                                      .Aggregate((ulong?)0, (a, b) => a + b);

            // BUG: Technically, we're casting a ulong to a long, but we don't expect
            // 9 exabyte steams, so low risk.
            if (streamLength.HasValue)
            {
                CheckResourceGovernor((long)streamLength.Value);
            }

            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
            var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, memoryStream);
            foreach (var extractedFile in ExtractFile(newFileEntry))
            {
                yield return extractedFile;
            }
        }

        /// <summary>
        /// Extracts an BZip2 file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractBZip2File(FileEntry fileEntry)
        {
            using var bzip2Stream = new BZip2Stream(fileEntry.Content, SharpCompress.Compressors.CompressionMode.Decompress, false);
            using var memoryStream = new MemoryStream();
            CheckResourceGovernor((long)bzip2Stream.Length);
            bzip2Stream.CopyTo(memoryStream);

            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
            var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, memoryStream);
            foreach (var extractedFile in ExtractFile(newFileEntry))
            {
                yield return extractedFile;
            }
        }

        /// <summary>
        /// Extracts a RAR file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractRarFile(FileEntry fileEntry)
        {

            using var rarArchive = RarArchive.Open(fileEntry.Content);
            foreach (var entry in rarArchive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                CheckResourceGovernor((long)entry.Size);
                var newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, entry.OpenEntryStream());
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }

        /// <summary>
        /// Extracts a 7-Zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> Extract7ZipFile(FileEntry fileEntry)
        {
            using var sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
            foreach (var entry in sevenZipArchive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                CheckResourceGovernor(entry.Size);
                var newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, entry.OpenEntryStream());
                foreach (var extractedFile in ExtractFile(newFileEntry))
                {
                    yield return extractedFile;
                }
            }
        }

        /// <summary>
        /// Extracts an .ar (deb) file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractArFile(FileEntry fileEntry)
        {
            foreach (var entry in ArArchiveFile.GetFileEntries(fileEntry))
            {
                using var memoryStream = new MemoryStream();
                CheckResourceGovernor((long)entry.Content.Length);
                foreach (var extractedFile in ExtractFile(entry))
                {
                    yield return extractedFile;
                }
            }
        }
    }
}
