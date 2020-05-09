// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CST.OpenSource.MultiExtractor;
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
            var results = extractor.ExtractFile(path, parallel);
            Assert.IsTrue(results.Count() == expectedNumFiles);
        }
    }
}
