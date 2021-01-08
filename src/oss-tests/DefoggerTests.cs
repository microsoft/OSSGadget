using Microsoft.CST.OpenSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Markup;

namespace osstests
{
    [TestClass]
    public class DefoggerTests
    {
        string decoded = "The quick brown fox jumped over the lazy dog.";

        public DefoggerTests()
        {
        }

        [TestMethod]
        public void DetectHex()
        {
            var encoded = BitConverter.ToString(Encoding.Default.GetBytes(decoded)).Replace("-", "");

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectHexTest", encoded);
            Assert.AreEqual(1, tool.Findings.Count);
            Assert.IsTrue(tool.Findings.Any(x => x.EncodedText == encoded && x.DecodedText == decoded));

        }

        [TestMethod]
        public void DetectHexWithDash()
        {
            var encodedWithDash = BitConverter.ToString(Encoding.Default.GetBytes(decoded));

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectHexTest", encodedWithDash);
            Assert.AreEqual(1, tool.Findings.Count);
            Assert.IsTrue(tool.Findings.Any(x => x.EncodedText == encodedWithDash && x.DecodedText == decoded));
        }

        [TestMethod]
        public void DetectBase64()
        {
            var base64 = Convert.ToBase64String(Encoding.Default.GetBytes(decoded));

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectBase64Test", base64);
            Assert.AreEqual(1, tool.Findings.Count);
            Assert.IsTrue(tool.Findings.Any(x => x.EncodedText == base64 && x.DecodedText == decoded));
        }

        [TestMethod]
        public void DetectZip()
        {
            var zip = new FileStream(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "Base64Zip.zip"), FileMode.Open);
            var ms = new MemoryStream();
            zip.CopyTo(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectZipTest", base64);

            Assert.AreEqual(2, tool.Findings.Count);
            Assert.AreEqual(2,tool.Findings.Count(x => x.DecodedText.Equals(decoded)));
            Assert.AreEqual(2, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64));
            Assert.AreEqual(1, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex));

            Assert.AreEqual(1, tool.ArchiveFindings.Count);
            Assert.AreEqual(1, tool.BinaryFindings.Count);
        }

        [TestMethod]
        public void DetectBinaryTest()
        {
            var bin = new FileStream(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData","oss-defog.dll"),FileMode.Open);
            var ms = new MemoryStream();
            bin.CopyTo(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectBinaryTest", base64);
            Assert.AreEqual(1, tool.BinaryFindings.Count);
        }
    }
}
