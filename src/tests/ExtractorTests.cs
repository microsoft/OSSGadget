// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CST.OpenSource.MultiExtractor;
using System.IO;
using System.Linq;
using System;

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
        public void ExtractArchive(string fileName, bool parallel, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, parallel);
            Assert.IsTrue(results.Count() == expectedNumFiles);
        }

        [DataTestMethod]
        [DataRow("Nested.Zip", false, 26 * 9)]
        [DataRow("Nested.Zip", true, 26 * 9)]

        public void ExtractNestedArchive(string fileName, bool parallel, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            Assert.IsTrue(extractor.ExtractFile(path, parallel).Count() == expectedNumFiles);
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
        public void TestQuineBombs(string fileName, bool parallel)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            try
            {
                // Either we should get exactly 1 result and its the thing we passed in
                // This happens when the exception gets triggered inside the extractor
                // (In parallel we fully generate the list in memory)
                var results = extractor.ExtractFile(path, parallel).ToList();
                Assert.IsTrue(results.Count == 1);
                Assert.IsTrue(results[0].FullPath == path);
                return;
            }
            // Or we should throw one of these overflow exceptions which occur when we are iterating
            catch (Exception e) when (
                    e is OverflowException
                    || e is TimeoutException
                    || e is IOException)
            {
                return;
            }
        }
    }
}
