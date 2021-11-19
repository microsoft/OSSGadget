// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Microsoft.CST.OpenSource.Tests

{
    [TestClass]
    public class DefoggerTests
    {
        readonly string decoded = "The quick brown fox jumped over the lazy dog.";

        public DefoggerTests()
        {
        }

        [TestMethod]
        public void DetectHex()
        {
            string? encoded = BitConverter.ToString(Encoding.Default.GetBytes(decoded)).Replace("-", "");

            DefoggerTool? tool = new();
            tool.AnalyzeFile("DetectHexTest", encoded);
            Assert.AreEqual(2, tool.Findings.Count);
            Assert.IsTrue(tool.Findings.Any(x => x.EncodedText == encoded && x.DecodedText == decoded));

        }

        [TestMethod]
        public void DetectHexWithDash()
        {
            string? encodedWithDash = BitConverter.ToString(Encoding.Default.GetBytes(decoded));

            DefoggerTool? tool = new();
            tool.AnalyzeFile("DetectHexTest", encodedWithDash);
            Assert.AreEqual(1, tool.Findings.Count);
            Assert.IsTrue(tool.Findings.Any(x => x.EncodedText == encodedWithDash && x.DecodedText == decoded));
        }

        [TestMethod]
        public void DetectBase64()
        {
            string? base64 = Convert.ToBase64String(Encoding.Default.GetBytes(decoded));

            DefoggerTool? tool = new();
            tool.AnalyzeFile("DetectBase64Test", base64);
            Assert.AreEqual(1, tool.Findings.Count);
            Assert.IsTrue(tool.Findings.Any(x => x.EncodedText == base64 && x.DecodedText == decoded));
        }

        [TestMethod]
        public void DetectNested()
        {
            string? nested = Convert.ToHexString(Encoding.Default.GetBytes(Convert.ToBase64String(Encoding.Default.GetBytes(decoded))));

            DefoggerTool? tool = new();
            tool.AnalyzeFile("DetectNestedTest", nested);
            Assert.AreEqual(3, tool.Findings.Count);
            Assert.AreEqual(2, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64));
            Assert.AreEqual(1, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex));
            Assert.IsTrue(tool.Findings.Any(x => x.DecodedText == decoded));
        }

        [TestMethod]
        public void DetectNestedZip()
        {
            FileStream? zip = new(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "Base64Zip.zip"), FileMode.Open);
            MemoryStream? ms = new();
            zip.CopyTo(ms);
            string? nested = Convert.ToHexString(Encoding.Default.GetBytes(Convert.ToBase64String(ms.ToArray())));

            DefoggerTool? tool = new();
            tool.AnalyzeFile("DetectNestedZipTest", nested);
            Assert.AreEqual(5, tool.Findings.Count);
            Assert.AreEqual(3, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64));
            Assert.AreEqual(2, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex));
            Assert.AreEqual(1, tool.ArchiveFindings.Count);
            Assert.AreEqual(1, tool.BinaryFindings.Count);
            Assert.IsTrue(tool.Findings.Any(x => x.DecodedText == decoded));
            zip.Close();
        }

        [TestMethod]
        public void DetectZip()
        {
            FileStream? zip = new(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "Base64Zip.zip"), FileMode.Open);
            MemoryStream? ms = new();
            zip.CopyTo(ms);
            string? base64 = Convert.ToBase64String(ms.ToArray());

            DefoggerTool? tool = new();
            tool.AnalyzeFile("DetectZipTest", base64);

            Assert.AreEqual(3, tool.Findings.Count);
            Assert.AreEqual(2, tool.Findings.Count(x => x.DecodedText.Equals(decoded)));
            Assert.AreEqual(2, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64));
            Assert.AreEqual(1, tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex));

            Assert.AreEqual(1, tool.ArchiveFindings.Count);
            Assert.AreEqual(1, tool.BinaryFindings.Count);
            zip.Close();
        }

        [TestMethod]
        public void DetectBinaryTest()
        {
            FileStream? bin = new(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "oss-defog.dll"), FileMode.Open);
            MemoryStream? ms = new();
            bin.CopyTo(ms);
            string? base64 = Convert.ToBase64String(ms.ToArray());

            DefoggerTool? tool = new();
            tool.AnalyzeFile("DetectBinaryTest", base64);
            Assert.AreEqual(1, tool.BinaryFindings.Count);
        }
    }
}
