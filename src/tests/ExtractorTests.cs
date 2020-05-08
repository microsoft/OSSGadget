using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CST.OpenSource.Shared;
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
        [DataRow("Shared.deb", false)]
        [DataRow("Shared.deb", true)]
        public void ExtractArchive(string fileName, bool parallel)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, parallel);
            Assert.IsTrue(results.Count() == 26);
        }

        [DataTestMethod]
        [DataRow("Nested.Zip", false)]
        [DataRow("Nested.Zip", true)]

        public void ExtractNestedArchive(string fileName, bool parallel)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            // 26 each times the number of sub archives
            Assert.IsTrue(extractor.ExtractFile(path, parallel).Count() == 26 * 9);
        }
    }
}
