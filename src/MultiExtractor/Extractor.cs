// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.Iso9660;
using DiscUtils.Ntfs;
using DiscUtils.Setup;
using DiscUtils.Streams;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;

namespace Microsoft.CST.OpenSource.MultiExtractor
{
    public class Extractor
    {
        /// <summary>
        /// Internal buffer size for extraction
        /// </summary>
        private const int BUFFER_SIZE = 32768;

        private const string DEBUG_STRING = "Failed parsing archive of type {0} {1}:{2} ({3})";

        /// <summary>
        /// The maximum number of items to take at once in the parallel extractors
        /// </summary>
        private const int MAX_BATCH_SIZE = 50;

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
        private readonly string IS_QUINE_STRING = "Detected Quine {0} in {1}. Aborting Extraction.";

        /// <summary>
        /// Times extraction operations to avoid denial of service.
        /// </summary>
        private Stopwatch GovernorStopwatch;

        /// <summary>
        /// Stores the number of bytes left before we abort (denial of service).
        /// </summary>
        private long CurrentOperationProcessedBytesLeft = -1;

        public Extractor()
        {   
            MaxExtractedBytesRatio = DEFAULT_MAX_EXTRACTED_BYTES_RATIO;
            GovernorStopwatch = new Stopwatch();
            SetupHelper.RegisterAssembly(typeof(FatFileSystem).Assembly);
            SetupHelper.RegisterAssembly(typeof(ExtFileSystem).Assembly);
            SetupHelper.RegisterAssembly(typeof(NtfsFileSystem).Assembly);
        }

        private void ResetResourceGovernor()
        {
            Logger.Trace("ResetResourceGovernor()");
            GovernorStopwatch.Reset();
            CurrentOperationProcessedBytesLeft = -1;
        }

        private void ResetResourceGovernor(Stream stream)
        {
            Logger.Trace("ResetResourceGovernor()");

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream must not be null.");
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
                throw new OverflowException("Too many bytes extracted, exceeding limit.");
            }
        }


        /// <summary>
        /// Extracts files from the file 'filename'.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        public IEnumerable<FileEntry> ExtractFile(string filename, bool parallel = false)
        {
           if (!File.Exists(filename))
            {
                Logger.Warn("ExtractFile called, but {0} does not exist.", filename);
                return Array.Empty<FileEntry>();
            }
            IEnumerable<FileEntry> result = Array.Empty<FileEntry>();
            FileStream? fs = null;
            try
            {
                fs = new FileStream(filename,FileMode.Open);
                ResetResourceGovernor(fs);
                result = ExtractFile(new FileEntry(filename, "", fs),parallel);
            }
            catch(Exception ex)
            {
                Logger.Debug(ex, "Failed to extract file {0}", filename);
            }
            fs?.Close();
            fs?.Dispose();
            return result;
        }

        /// <summary>
        /// Extracts files from the file, identified by 'filename', but with 
        /// contents passed through 'archiveBytes'. Note that 'filename' does not
        /// have to exist; it will only be used to identify files extracted.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        public IEnumerable<FileEntry> ExtractFile(string filename, byte[] archiveBytes, bool parallel = false)
        {
            using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            fs.Write(archiveBytes, 0, archiveBytes.Length);
            ResetResourceGovernor(fs);
            var result = ExtractFile(new FileEntry(filename, "", fs, true),parallel);
            return result;
        }

        /// <summary>
        /// Extracts files from the given FileEntry, using the appropriate
        /// extractors, recursively.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractFile(FileEntry fileEntry, bool parallel = false)
        {
            Logger.Trace("ExtractFile({0})", fileEntry.FullPath);
            CurrentOperationProcessedBytesLeft -= fileEntry.Content.Length;
            CheckResourceGovernor();
            IEnumerable<FileEntry> result;
            bool useRaw = false;

            try
            {
                var fileEntryType = MiniMagic.DetectFileType(fileEntry);
                switch (fileEntryType)
                {
                    case ArchiveFileType.ZIP:
                        result = ExtractZipFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.RAR:
                        result = ExtractRarFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.P7ZIP:
                        result = Extract7ZipFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.DEB:
                        result = ExtractDebFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.GZIP:
                        result = ExtractGZipFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.TAR:
                        result = ExtractTarFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.XZ:
                        result = ExtractXZFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.BZIP2:
                        result = ExtractBZip2File(fileEntry, parallel);
                        break;
                    case ArchiveFileType.GNU_AR:
                        result = ExtractGnuArFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.ISO_9660:
                        result = ExtractIsoFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.VHDX:
                        result = ExtractVHDXFile(fileEntry, parallel);
                        break;
                    case ArchiveFileType.VHD:
                        result = ExtractVHDFile(fileEntry, parallel);
                        break;
                    default:
                        useRaw = true;
                        result = new[] { fileEntry };
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error extracting {0}: {1}", fileEntry.FullPath, ex.Message);
                useRaw = true;
                result = new[] { fileEntry };   // Default is to not try to extract.
            }

            // After we are done with an archive subtract its bytes. Contents have been counted now separately
            if (!useRaw)
            {
                CurrentOperationProcessedBytesLeft += fileEntry.Content.Length;
            }

            return result;
        }

        /// <summary>
        /// Extracts an a VHDX file
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        private IEnumerable<FileEntry> ExtractVHDXFile(FileEntry fileEntry, bool parallel)
        {
            if (parallel)
            {
                foreach (var entry in ParallelExtractVHDXFile(fileEntry))
                {
                    yield return entry;
                }
                yield break;
            }
            using DiscUtils.Vhdx.DiskImageFile baseFile = new DiscUtils.Vhdx.DiskImageFile(fileEntry.Content);
            using var disk = new DiscUtils.Vhdx.Disk(new List<DiscUtils.Vhdx.DiskImageFile> { baseFile }, Ownership.Dispose);
            var manager = new VolumeManager(disk);
            var logicalVolumes = manager.GetLogicalVolumes();
            foreach (var volume in logicalVolumes)
            {
                var fsInfos = FileSystemManager.DetectFileSystems(volume);
                foreach (var fsInfo in fsInfos)
                {
                    using var fs = fsInfo.Open(volume);
                    foreach (var file in fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories))
                    {
                        CheckResourceGovernor(file.Length);
                        SparseStream? fileStream = null;
                        try
                        {
                            fileStream = fs.OpenFile(file, FileMode.Open);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(e, "Failed to open {0} in volume {1}", file, volume.Identity);
                        }
                        if (fileStream != null)
                        {
                            var newFileEntry = new FileEntry($"{volume.Identity}\\{file}", fileEntry.FullPath, fileStream);
                            var entries = ExtractFile(newFileEntry, parallel);
                            foreach (var entry in entries)
                            {
                                yield return entry;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts an a VHD file
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        private IEnumerable<FileEntry> ExtractVHDFile(FileEntry fileEntry, bool parallel)
        {
            if (parallel)
            {
                foreach (var entry in ParallelExtractVHDFile(fileEntry))
                {
                    yield return entry;
                }
                yield break;
            }
            using DiscUtils.Vhd.DiskImageFile baseFile = new DiscUtils.Vhd.DiskImageFile(fileEntry.Content);
            using var disk = new DiscUtils.Vhd.Disk(new List<DiscUtils.Vhd.DiskImageFile> { baseFile }, Ownership.Dispose);
            var manager = new VolumeManager(disk);
            var logicalVolumes = manager.GetLogicalVolumes();
            foreach (var volume in logicalVolumes)
            {
                var fsInfos = FileSystemManager.DetectFileSystems(volume);
                foreach (var fsInfo in fsInfos)
                {
                    using var fs = fsInfo.Open(volume);
                    foreach (var file in fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories))
                    {
                        CheckResourceGovernor(file.Length);
                        SparseStream? fileStream = null;
                        try
                        {
                            fileStream = fs.OpenFile(file, FileMode.Open);
                        }
                        catch(Exception e)
                        {
                            Logger.Debug(e, "Failed to open {0} in volume {1}", file, volume.Identity);
                        }
                        if (fileStream != null)
                        {
                            var newFileEntry = new FileEntry($"{volume.Identity}\\{file}", fileEntry.FullPath, fileStream);
                            var entries = ExtractFile(newFileEntry, parallel);
                            foreach (var entry in entries)
                            {
                                yield return entry;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts an an ISO file
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        private IEnumerable<FileEntry> ExtractIsoFile(FileEntry fileEntry, bool parallel)
        {
            if (parallel)
            {
                foreach (var entry in ParallelExtractIsoFile(fileEntry))
                {
                    yield return entry;
                }
                yield break;
            }
            using CDReader cd = new CDReader(fileEntry.Content, true);
            
            foreach(var file in cd.GetFiles(cd.Root.FullName,"*.*", SearchOption.AllDirectories))
            {
                var fileInfo = cd.GetFileInfo(file);
                CheckResourceGovernor(fileInfo.Length);
                Stream? stream = null;
                try
                {
                    stream = fileInfo.OpenRead();
                }
                catch (Exception e)
                {
                    Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.Name, fileEntry.FullPath, e.GetType(), e.Message);
                }
                if (stream != null)
                {
                    var newFileEntry = new FileEntry(fileInfo.Name, fileEntry.FullPath, stream);
                    var entries = ExtractFile(newFileEntry, parallel);
                    foreach (var entry in entries)
                    {
                        yield return entry;
                    }
                }
            }
        }


        /// <summary>
        /// Extracts an archive file created with GNU ar
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        private IEnumerable<FileEntry> ExtractGnuArFile(FileEntry fileEntry, bool parallel)
        {
            IEnumerable<FileEntry>? fileEntries = null;
            try
            {
                fileEntries = GnuArFile.GetFileEntries(fileEntry);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.GNU_AR, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (fileEntries != null)
            {
                foreach (var entry in fileEntries)
                {
                    CheckResourceGovernor(entry.Content.Length);
                    foreach (var extractedFile in ExtractFile(entry))
                    {
                        yield return extractedFile;
                    }
                }
            }
        }

        /// <summary>
        /// Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractZipFile(FileEntry fileEntry, bool parallel)
        {
            if (parallel)
            {
                foreach (var entry in ParallelExtractZipFile(fileEntry))
                {
                    yield return entry;
                }
                yield break;
            }
            ZipFile? zipFile = null;
            try
            {
                zipFile = new ZipFile(fileEntry.Content);
            }
            catch(Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipFile != null)
            {
                foreach (ZipEntry? zipEntry in zipFile)
                {
                    if (zipEntry is null ||
                        zipEntry.IsDirectory ||
                        zipEntry.IsCrypted ||
                        !zipEntry.CanDecompress)
                    {
                        continue;
                    }

                    using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                    try
                    {
                        byte[] buffer = new byte[BUFFER_SIZE];
                        var zipStream = zipFile.GetInputStream(zipEntry);
                        StreamUtils.Copy(zipStream, fs, buffer);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                    }

                    var newFileEntry = new FileEntry(zipEntry.Name, fileEntry.FullPath, fs);

                    if (AreIdentical(fileEntry, newFileEntry))
                    {
                        Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                        throw new OverflowException();
                    }

                    foreach (var extractedFile in ExtractFile(newFileEntry, parallel))
                    {
                        yield return extractedFile;
                    }
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
        private IEnumerable<FileEntry> ExtractGZipFile(FileEntry fileEntry, bool parallel)
        {
            GZipArchive? gzipArchive = null;
            try
            {
                gzipArchive = GZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.GZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (gzipArchive != null)
            {
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

                    FileEntry? newFileEntry = null;
                    try
                    {
                        newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, entry.OpenEntryStream());
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(DEBUG_STRING, ArchiveFileType.GZIP, fileEntry.FullPath, newFilename, e.GetType());
                    }
                    if (newFileEntry != null)
                    {
                        foreach (var extractedFile in ExtractFile(newFileEntry, parallel))
                        {
                            yield return extractedFile;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts a tar file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractTarFile(FileEntry fileEntry, bool parallel)
        {
            TarEntry tarEntry;
            TarInputStream? tarStream = null;
            try
            {
                tarStream = new TarInputStream(fileEntry.Content);
            }
            catch(Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (tarStream != null)
            {
                while ((tarEntry = tarStream.GetNextEntry()) != null)
                {
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }
                    using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                    CheckResourceGovernor(tarStream.Length);
                    try
                    {
                        tarStream.CopyEntryContents(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Name, e.GetType());
                    }

                    var newFileEntry = new FileEntry(tarEntry.Name, fileEntry.FullPath, fs, true);
                    
                    if (AreIdentical(fileEntry, newFileEntry))
                    {
                        Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                        throw new OverflowException();
                    }

                    foreach (var extractedFile in ExtractFile(newFileEntry, parallel))
                    {
                        yield return extractedFile;
                    }
                }
                tarStream.Dispose();
            }
            else
            {
                // If we couldn't parse it just return it
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts an .XZ file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractXZFile(FileEntry fileEntry, bool parallel)
        {
            using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            XZStream? xzStream = null;
            try
            {
                xzStream = new XZStream(fileEntry.Content);

                // SharpCompress does not expose metadata without a full read,
                // so we need to decompress first, and then abort if the bytes
                // exceeded the governor's capacity.
                xzStream.CopyTo(fs);

                var streamLength = xzStream.Index.Records?.Select(r => r.UncompressedSize)
                                          .Aggregate((ulong?)0, (a, b) => a + b);

                // BUG: Technically, we're casting a ulong to a long, but we don't expect
                // 9 exabyte steams, so low risk.
                if (streamLength.HasValue)
                {
                    CheckResourceGovernor((long)streamLength.Value);
                }
            }
            catch(Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.XZ, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (xzStream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, fs, true);

                if (AreIdentical(fileEntry, newFileEntry))
                {
                    Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    throw new OverflowException();
                }

                foreach (var extractedFile in ExtractFile(newFileEntry, parallel))
                {
                    yield return extractedFile;
                }
                xzStream.Dispose();
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts an BZip2 file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractBZip2File(FileEntry fileEntry, bool parallel)
        {
            using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            BZip2Stream? bzip2Stream = null;
            try
            {
                bzip2Stream = new BZip2Stream(fileEntry.Content, SharpCompress.Compressors.CompressionMode.Decompress, false);
                CheckResourceGovernor(bzip2Stream.Length);
                bzip2Stream.CopyTo(fs);
            }
            catch(Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.BZIP2, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (bzip2Stream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = new FileEntry(newFilename, fileEntry.FullPath, fs, true);

                if (AreIdentical(fileEntry, newFileEntry))
                {
                    Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    throw new OverflowException();
                }

                foreach (var extractedFile in ExtractFile(newFileEntry, parallel))
                {
                    yield return extractedFile;
                }
                bzip2Stream.Dispose();
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts a RAR file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractRarFile(FileEntry fileEntry, bool parallel)
        {
            // TODO: This produces unpredictable results when run on Azure Pipelines, but cannot reproduce locally
            //if (parallel)
            //{
            //    foreach (var entry in ParallelExtractRarFile(fileEntry))
            //    {
            //        yield return entry;
            //    }
            //    yield break;
            //}
            RarArchive? rarArchive = null;
            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (rarArchive != null)
            {
                foreach (var entry in rarArchive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        continue;
                    }
                    CheckResourceGovernor(entry.Size);
                    FileEntry? newFileEntry = null;
                    try
                    {
                        newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, entry.OpenEntryStream());
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, entry.Key, e.GetType());
                    }
                    if (newFileEntry != null)
                    {
                        if (AreIdentical(fileEntry, newFileEntry))
                        {
                            Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }
                        foreach (var extractedFile in ExtractFile(newFileEntry, parallel))
                        {
                            yield return extractedFile;
                        }
                    }
                }
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts a 7-Zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> Extract7ZipFile(FileEntry fileEntry, bool parallel)
        {
            if (parallel)
            {
                foreach (var entry in ParallelExtract7ZipFile(fileEntry))
                {
                    yield return entry;
                }
                yield break;
            }
            SevenZipArchive? sevenZipArchive = null;
            try
            {
                sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (sevenZipArchive != null)
            {
                foreach (var entry in sevenZipArchive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        continue;
                    }
                    CheckResourceGovernor(entry.Size);
                    FileEntry? newFileEntry = null;
                    try
                    {
                         newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, entry.OpenEntryStream());
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.Key, e.GetType());
                    }
                    if (newFileEntry != null)
                    {
                        if (AreIdentical(fileEntry, newFileEntry))
                        {
                            Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }
                        foreach (var extractedFile in ExtractFile(newFileEntry, parallel))
                        {
                            yield return extractedFile;
                        }
                    }
                }
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts a .deb file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ExtractDebFile(FileEntry fileEntry, bool parallel)
        {
            if (parallel)
            {
                foreach (var entry in ParallelExtractDebFile(fileEntry))
                {
                    yield return entry;
                }
                yield break;
            }
            IEnumerable<FileEntry>? fileEntries = null;
            try
            {
                fileEntries = DebArchiveFile.GetFileEntries(fileEntry);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.DEB, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (fileEntries != null)
            {
                foreach (var entry in fileEntries)
                {
                    if (entry.Name == "control.tar.xz")
                    {
                        // This is control information for debian and not part of the actual files
                        continue;
                    }
                    CheckResourceGovernor(entry.Content.Length);
                    foreach (var extractedFile in ExtractFile(entry, parallel))
                    {
                        yield return extractedFile;
                    }
                }
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts a RAR file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ParallelExtractRarFile(FileEntry fileEntry)
        {
            List<FileEntry> files = new List<FileEntry>();
            RarArchive? rarArchive = null;
            List<RarArchiveEntry> entries = new List<RarArchiveEntry>();
            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
                entries.AddRange(rarArchive.Entries);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (!entries.Any())
            {
                yield return fileEntry;
            }
            while (entries.Any())
            {
                int batchSize = Math.Min(MAX_BATCH_SIZE, entries.Count());
                entries.GetRange(0, batchSize).AsParallel().ForAll(entry =>
                {
                    if (!entry.IsDirectory && !entry.IsEncrypted && entry.IsComplete)
                    {
                        CheckResourceGovernor(entry.Size);
                        try
                        {
                            var stream = entry.OpenEntryStream();
                            var newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, stream);
                            if (AreIdentical(fileEntry, newFileEntry))
                            {
                                Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                CurrentOperationProcessedBytesLeft = -1;
                            }
                            else
                            {
                                files.AddRange(ExtractFile(newFileEntry, true));
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, entry.Key, e.GetType());
                        }
                    }
                });
                CheckResourceGovernor(0);
                entries.RemoveRange(0, batchSize);

                while (files.Count > 0)
                {
                    yield return files[0];
                    files.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ParallelExtractZipFile(FileEntry fileEntry)
        {
            List<FileEntry> files = new List<FileEntry>();

            ZipFile? zipFile = null;
            try
            {
                zipFile = new ZipFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipFile != null)
            {
                var zipEntries = new List<ZipEntry>();
                foreach (ZipEntry? zipEntry in zipFile)
                {
                    if (zipEntry is null ||
                        zipEntry.IsDirectory ||
                        zipEntry.IsCrypted ||
                        !zipEntry.CanDecompress)
                    {
                        continue;
                    }
                    zipEntries.Add(zipEntry);
                }

                while (zipEntries.Count > 0)
                {
                    int batchSize = Math.Min(MAX_BATCH_SIZE, zipEntries.Count);
                    zipEntries.GetRange(0,batchSize).AsParallel().ForAll(zipEntry =>
                    {
                        CheckResourceGovernor(zipEntry.Size);
                        try
                        {
                            using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                            byte[] buffer = new byte[BUFFER_SIZE];
                            var zipStream = zipFile.GetInputStream(zipEntry);
                            StreamUtils.Copy(zipStream, fs, buffer);
                            var newFileEntry = new FileEntry(zipEntry.Name, fileEntry.FullPath, fs, true);
                            if (AreIdentical(fileEntry, newFileEntry))
                            {
                                Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                CurrentOperationProcessedBytesLeft = -1;
                            }
                            else
                            {
                                files.AddRange(ExtractFile(newFileEntry, true));
                            }
                        }
                        catch(Exception e)
                        {
                            Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                        }
                    });
                    CheckResourceGovernor(0);
                    zipEntries.RemoveRange(0, batchSize);
                    
                    while (files.Count > 0)
                    {
                        yield return files[0];
                        files.RemoveAt(0);
                    }
                }
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts a 7-Zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ParallelExtract7ZipFile(FileEntry fileEntry)
        {
            SevenZipArchive? sevenZipArchive = null;
            List<FileEntry> files = new List<FileEntry>();
            try
            {
                sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (sevenZipArchive != null)
            {
                var entries = sevenZipArchive.Entries.ToList();
                while (entries.Count() > 0)
                {
                    int batchSize = Math.Min(MAX_BATCH_SIZE, entries.Count());
                    entries.GetRange(0, batchSize).AsParallel().ForAll(entry =>
                    {
                        if (!entry.IsDirectory && !entry.IsEncrypted)
                        {
                            CheckResourceGovernor(entry.Size);
                            try
                            {
                                var stream = entry.OpenEntryStream();
                                var newFileEntry = new FileEntry(entry.Key, fileEntry.FullPath, stream);
                                if (AreIdentical(fileEntry, newFileEntry))
                                {
                                    Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                    CurrentOperationProcessedBytesLeft = -1;
                                }
                                else
                                {
                                    files.AddRange(ExtractFile(newFileEntry, true));
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.Key, e.GetType());
                            }
                        }
                    });
                    CheckResourceGovernor(0);
                    entries.RemoveRange(0, batchSize);

                    while (files.Count > 0)
                    {
                        yield return files[0];
                        files.RemoveAt(0);
                    }
                }
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts a .deb file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ParallelExtractDebFile(FileEntry fileEntry)
        {
            List<FileEntry> files = new List<FileEntry>();
            IEnumerable<FileEntry>? fileEntries = null;
            try
            {
                fileEntries = DebArchiveFile.GetFileEntries(fileEntry);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.DEB, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (fileEntries != null)
            {
                var entries = fileEntries.ToList();
                while (entries.Any())
                {
                    int batchSize = Math.Min(MAX_BATCH_SIZE, entries.Count());
                    entries.GetRange(0, batchSize).AsParallel().ForAll(entry =>
                    {
                        // This is control information for Debian's installer wizardy and not part of the actual files
                        if (entry.Name != "control.tar.xz")
                        {
                            CheckResourceGovernor(entry.Content.Length);
                            files.AddRange(ExtractFile(entry, true));
                        }
                    });
                    entries.RemoveRange(0, batchSize);

                    while (files.Count > 0)
                    {
                        yield return files[0];
                        files.RemoveAt(0);
                    }
                }
            }
            else
            {
                yield return fileEntry;
            }
        }

        /// <summary>
        /// Extracts an iso file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry">FileEntry to extract</param>
        /// <returns>Extracted files</returns>
        private IEnumerable<FileEntry> ParallelExtractIsoFile(FileEntry fileEntry)
        {
            List<FileEntry> files = new List<FileEntry>();

            using CDReader cd = new CDReader(fileEntry.Content, true);
            var cdFiles = cd.GetFiles(cd.Root.FullName, "*.*", SearchOption.AllDirectories).ToList();
            while (cdFiles.Count > 0)
            {
                int batchSize = Math.Min(MAX_BATCH_SIZE, cdFiles.Count);
                cdFiles.GetRange(0, batchSize).AsParallel().ForAll(cdFile =>
                {
                    var fileInfo = cd.GetFileInfo(cdFile);
                    try
                    {
                        CheckResourceGovernor(fileInfo.Length);
                        var newFileEntry = new FileEntry(fileInfo.Name, fileEntry.FullPath, fileInfo.OpenRead());
                        var entries = ExtractFile(newFileEntry, true);
                        files.AddRange(entries);
                    }
                    catch(Exception e)
                    {
                        Logger.Debug("Failed to extract {0} from ISO {1}. ({2})", fileInfo.Name, fileEntry.FullPath, e.GetType());
                    }
                });
                cdFiles.RemoveRange(0, batchSize);

                while (files.Count > 0)
                {
                    yield return files[0];
                    files.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Extracts an a VHD file
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        private IEnumerable<FileEntry> ParallelExtractVHDFile(FileEntry fileEntry)
        {
            List<FileEntry> files = new List<FileEntry>();

            using DiscUtils.Vhd.DiskImageFile baseFile = new DiscUtils.Vhd.DiskImageFile(fileEntry.Content);
            using var disk = new DiscUtils.Vhd.Disk(new List<DiscUtils.Vhd.DiskImageFile> { baseFile }, Ownership.Dispose);
            var manager = new VolumeManager(disk);
            var logicalVolumes = manager.GetLogicalVolumes();
            foreach (var volume in logicalVolumes)
            {
                var fsInfos = FileSystemManager.DetectFileSystems(volume);
                foreach (var fsInfo in fsInfos)
                {
                    using var fs = fsInfo.Open(volume);
                    var diskFiles = fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories).ToList();

                    while (diskFiles.Count > 0)
                    {
                        int batchSize = Math.Min(MAX_BATCH_SIZE, diskFiles.Count);
                        diskFiles.GetRange(0, batchSize).AsParallel().ForAll(file =>
                        {
                            CheckResourceGovernor(file.Length);
                            SparseStream? fileStream = null;
                            try
                            {
                                fileStream = fs.OpenFile(file, FileMode.Open);
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(e, "Failed to open {0} in volume {1}", file, volume.Identity);
                            }
                            if (fileStream != null)
                            {
                                var newFileEntry = new FileEntry($"{volume.Identity}\\{file}", fileEntry.FullPath, fileStream);
                                var entries = ExtractFile(newFileEntry, true);
                                files.AddRange(entries);
                            }
                        });
                        diskFiles.RemoveRange(0, batchSize);

                        while (files.Count > 0)
                        {
                            yield return files[0];
                            files.RemoveAt(0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts an a VHDX file
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        private IEnumerable<FileEntry> ParallelExtractVHDXFile(FileEntry fileEntry)
        {
            List<FileEntry> files = new List<FileEntry>();

            using DiscUtils.Vhdx.DiskImageFile baseFile = new DiscUtils.Vhdx.DiskImageFile(fileEntry.Content);
            using var disk = new DiscUtils.Vhdx.Disk(new List<DiscUtils.Vhdx.DiskImageFile> { baseFile }, Ownership.Dispose);
            var manager = new VolumeManager(disk);
            var logicalVolumes = manager.GetLogicalVolumes();
            foreach (var volume in logicalVolumes)
            {
                var fsInfos = FileSystemManager.DetectFileSystems(volume);
                foreach (var fsInfo in fsInfos)
                {
                    using var fs = fsInfo.Open(volume);
                    var diskFiles = fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories).ToList();

                    while (diskFiles.Count > 0)
                    {
                        int batchSize = Math.Min(MAX_BATCH_SIZE, diskFiles.Count);
                        diskFiles.GetRange(0, batchSize).AsParallel().ForAll(file =>
                        {
                            CheckResourceGovernor(file.Length);
                            SparseStream? fileStream = null;
                            try
                            {
                                fileStream = fs.OpenFile(file, FileMode.Open);
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(e, "Failed to open {0} in volume {1}", file, volume.Identity);
                            }
                            if (fileStream != null)
                            {
                                var newFileEntry = new FileEntry($"{volume.Identity}\\{file}", fileEntry.FullPath, fileStream);
                                var entries = ExtractFile(newFileEntry, true);
                                files.AddRange(entries);
                            }
                        });
                        diskFiles.RemoveRange(0, batchSize);

                        while (files.Count > 0)
                        {
                            yield return files[0];
                            files.RemoveAt(0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if the two files are identical (i.e. Extraction is a quine)
        /// </summary>
        /// <param name="fileEntry1"></param>
        /// <param name="fileEntry2"></param>
        /// <returns></returns>
        public static bool AreIdentical(FileEntry fileEntry1, FileEntry fileEntry2)
        {
            if (fileEntry1.Name == fileEntry2.Name && fileEntry1.Content.Length == fileEntry2.Content.Length)
            {
                var buffer1 = new Span<byte>(new byte[1024]);
                var buffer2 = new Span<byte>(new byte[1024]);
                var stream1 = fileEntry1.Content;
                var stream2 = fileEntry2.Content;
                var position1 = fileEntry1.Content.Position;
                var position2 = fileEntry2.Content.Position;
                stream1.Position = 0;
                stream2.Position = 0;
                var bytesRemaining = stream2.Length;
                while (bytesRemaining > 0)
                {
                    stream1.Read(buffer1);
                    stream2.Read(buffer2);
                    if (!buffer1.SequenceEqual(buffer2))
                    {
                        stream1.Position = position1;
                        stream2.Position = position2;
                        return false;
                    }
                    bytesRemaining = stream2.Length - stream2.Position;
                }
                stream1.Position = position1;
                stream2.Position = position2;
                return true;
            }
            return false;
        }
    }
}
