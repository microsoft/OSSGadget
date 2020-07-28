// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class ExtractorTests
    {
        [DataTestMethod]
        [DataRow("Shared.zip", false)]
        [DataRow("Shared.zip", true)]
        [DataRow("Shared.7z", false)]
        [DataRow("Shared.7z", true)]
        [DataRow("Shared.Tar", false)]
        [DataRow("Shared.Tar", true)]
        [DataRow("Shared.rar", false)]
        [DataRow("Shared.rar", true)]
        [DataRow("Shared.rar4", false)]
        [DataRow("Shared.rar4", true)]
        [DataRow("Shared.tar.bz2", false)]
        [DataRow("Shared.tar.bz2", true)]
        [DataRow("Shared.tar.gz", false)]
        [DataRow("Shared.tar.gz", true)]
        [DataRow("Shared.tar.xz", false)]
        [DataRow("Shared.tar.xz", true)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", true, 6)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", false, 6)]
        [DataRow("Shared.a", false, 1)]
        [DataRow("Shared.a", true, 1)]
        [DataRow("Shared.deb", false)]
        [DataRow("Shared.deb", true)]
        [DataRow("Shared.ar", false)]
        [DataRow("Shared.ar", true)]
        [DataRow("Shared.iso", false)]
        [DataRow("Shared.iso", true)]
        [DataRow("Shared.vhd", false, 29)] // 26 + Some invisible system files
        [DataRow("Shared.vhd", true, 29)]
        [DataRow("Shared.vhdx", false)]
        [DataRow("Shared.vhdx", true)]
        [DataRow("Shared.wim", false)]
        [DataRow("Shared.wim", true)]
        [DataRow("Empty.vmdk", false, 0)]
        [DataRow("Empty.vmdk", true, 0)]
        [DataRow("TextFile.md", false, 1)]
        [DataRow("TextFile.md", true, 1)]
        public void ExtractArchive(string fileName, bool parallel, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, parallel).ToList();
            Assert.IsTrue(results.Count() == expectedNumFiles);
        }

        [DataTestMethod]
        [DataRow("Shared.zip", false)]
        [DataRow("Shared.zip", true)]
        [DataRow("Shared.7z", false)]
        [DataRow("Shared.7z", true)]
        [DataRow("Shared.Tar", false)]
        [DataRow("Shared.Tar", true)]
        [DataRow("Shared.rar", false)]
        [DataRow("Shared.rar", true)]
        [DataRow("Shared.rar4", false)]
        [DataRow("Shared.rar4", true)]
        [DataRow("Shared.tar.bz2", false)]
        [DataRow("Shared.tar.bz2", true)]
        [DataRow("Shared.tar.gz", false)]
        [DataRow("Shared.tar.gz", true)]
        [DataRow("Shared.tar.xz", false)]
        [DataRow("Shared.tar.xz", true)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", true, 6)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", false, 6)]
        [DataRow("Shared.a", false, 1)]
        [DataRow("Shared.a", true, 1)]
        [DataRow("Shared.deb", false)]
        [DataRow("Shared.deb", true)]
        [DataRow("Shared.ar", false)]
        [DataRow("Shared.ar", true)]
        [DataRow("Shared.iso", false)]
        [DataRow("Shared.iso", true)]
        [DataRow("Shared.vhd", false, 29)] // 26 + Some invisible system files
        [DataRow("Shared.vhd", true, 29)]
        [DataRow("Shared.vhdx", false)]
        [DataRow("Shared.vhdx", true)]
        [DataRow("Shared.wim", false)]
        [DataRow("Shared.wim", true)]
        [DataRow("Empty.vmdk", false, 0)]
        [DataRow("Empty.vmdk", true, 0)]
        [DataRow("TextFile.md", false, 1)]
        [DataRow("TextFile.md", true, 1)]
        public void ExtractArchiveFromStream(string fileName, bool parallel, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            using var stream = new FileStream(path, FileMode.Open);
            var results = extractor.ExtractStream(path, stream, parallel).ToList();
            Assert.IsTrue(results.Count() == expectedNumFiles);
            stream.Close();
        }

        [DataTestMethod]
        [DataRow("Nested.Zip", false, 26 * 8)]
        [DataRow("Nested.Zip", true, 26 * 8)]
        public void ExtractNestedArchive(string fileName, bool parallel, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, parallel);
            Assert.IsTrue(results.Count() == expectedNumFiles);
        }

        [DataTestMethod]
        [DataRow("Shared.zip", ArchiveFileType.ZIP)]
        [DataRow("Shared.7z", ArchiveFileType.P7ZIP)]
        [DataRow("Shared.Tar", ArchiveFileType.TAR)]
        [DataRow("Shared.rar", ArchiveFileType.RAR)]
        [DataRow("Shared.rar4", ArchiveFileType.RAR)]
        [DataRow("Shared.tar.bz2", ArchiveFileType.BZIP2)]
        [DataRow("Shared.tar.gz", ArchiveFileType.GZIP)]
        [DataRow("Shared.tar.xz", ArchiveFileType.XZ)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", ArchiveFileType.DEB)]
        [DataRow("Shared.a", ArchiveFileType.UNKNOWN)]
        [DataRow("Shared.deb", ArchiveFileType.DEB)]
        [DataRow("Shared.ar", ArchiveFileType.AR)]
        [DataRow("Shared.iso", ArchiveFileType.ISO_9660)]
        [DataRow("Shared.vhd", ArchiveFileType.VHD)]
        [DataRow("Shared.vhdx", ArchiveFileType.VHDX)]
        [DataRow("Shared.wim", ArchiveFileType.WIM)]
        [DataRow("Empty.vmdk", ArchiveFileType.VMDK)]
        [DataRow("TextFile.md", ArchiveFileType.UNKNOWN)]
        public void TestMiniMagic(string fileName, ArchiveFileType expectedArchiveFileType)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            using FileStream fs = new FileStream(path, FileMode.Open);
            // Test just based on the content
            var fileEntry = new FileEntry("NoName", fs);

            Assert.IsTrue(MiniMagic.DetectFileType(fileEntry) == expectedArchiveFileType);
            Assert.IsTrue(fileEntry.Content.Position == 0);

            // Should also work if the stream doesn't start at 0
            fileEntry.Content.Position = 10;
            Assert.IsTrue(MiniMagic.DetectFileType(fileEntry) == expectedArchiveFileType);
            Assert.IsTrue(fileEntry.Content.Position == 10);

            // We should also detect just on file names if the content doesn't match
            var nameOnlyEntry = new FileEntry(fileName, new MemoryStream());
            Assert.IsTrue(MiniMagic.DetectFileType(nameOnlyEntry) == expectedArchiveFileType);
        }

        [DataTestMethod]
        [DataRow("droste.zip", false)]
        [DataRow("droste.zip", true)]
        [DataRow("10GB.7z.bz2", false)]
        [DataRow("10GB.7z.bz2", true)]
        [DataRow("10GB.gz.bz2", false)]
        [DataRow("10GB.gz.bz2", true)]
        [DataRow("10GB.rar.bz2", false)]
        [DataRow("10GB.rar.bz2", true)]
        [DataRow("10GB.xz.bz2", false)]
        [DataRow("10GB.xz.bz2", true)]
        [DataRow("10GB.zip.bz2", false)]
        [DataRow("10GB.zip.bz2", true)]
        [DataRow("zblg.zip", false)]
        [DataRow("zblg.zip", true)]
        [DataRow("zbsm.zip", false)]
        [DataRow("zbsm.zip", true)]
        [DataRow("zbxl.zip", false)]
        [DataRow("zbxl.zip", true)]
        public void TestQuineBombs(string fileName, bool parallel)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            IEnumerable<FileEntry> results;
            try
            {
                results = extractor.ExtractFile(path, parallel).ToList();
                // Getting here means we didnt catch the bomb
            }
            // We should throw an overflow exception when we detect a quine or bomb
            catch (Exception e) when (
                    e is OverflowException)
            {
                return;
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Shouldn't hit other exceptions in this test.");
            }
            // Getting here means we didnt catch the bomb
            Assert.Fail();
        }

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}