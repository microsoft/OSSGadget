using Microsoft.CST.OpenSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
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
            if (tool.Findings.Count != 1)
            {
                Assert.Fail("Only expecting one finding.");
            }
            if (!tool.Findings.Any(x => x.EncodedText == encoded && x.DecodedText == decoded))
            {
                Assert.Fail("Did not detect and decode properly.");
            }

        }

        [TestMethod]
        public void DetectHexWithDash()
        {
            var encodedWithDash = BitConverter.ToString(Encoding.Default.GetBytes(decoded));

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectHexTest", encodedWithDash);
            if (tool.Findings.Count != 1)
            {
                Assert.Fail("Only expecting one finding.");
            }
            if (!tool.Findings.Any(x => x.EncodedText == encodedWithDash && x.DecodedText == decoded))
            {
                Assert.Fail("Did not detect and decode properly.");
            }
        }

        [TestMethod]
        public void DetectBase64()
        {
            var base64 = Convert.ToBase64String(Encoding.Default.GetBytes(decoded));

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectBase64Test", base64);
            if (tool.Findings.Count != 1)
            {
                Assert.Fail("Only expecting one finding.");
            }
            if (!tool.Findings.Any(x => x.EncodedText == base64 && x.DecodedText == decoded))
            {
                Assert.Fail("Did not detect and decode properly.");
            }
        }

        [TestMethod]
        public void DetectZip()
        {
            var zip = new FileStream(Path.Combine("TestData", "Base64Zip.zip"), FileMode.Open);
            var ms = new MemoryStream();
            zip.CopyTo(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectZipTest", base64);

            Assert.AreEqual(tool.Findings.Count, 2);
            Assert.IsTrue(tool.Findings.All(x => x.DecodedText.Equals(decoded)),"Expected all findings to be decoded properly.");
            Assert.AreEqual(tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64),1);
            Assert.AreEqual(tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex), 1);

            Assert.AreEqual(tool.ArchiveFindings.Count, 1);
            Assert.AreEqual(tool.BinaryFindings.Count, 1);
        }

        [TestMethod]
        public void DetectBinaryTest()
        {
            var bin = new FileStream(Path.Combine("TestData","oss-defog.dll"),FileMode.Open);
            var ms = new MemoryStream();
            bin.CopyTo(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var tool = new DefoggerTool();
            tool.AnalyzeFile("DetectBinaryTest", base64);
            Assert.AreEqual(tool.BinaryFindings.Count, 1);
        }
    }
}
