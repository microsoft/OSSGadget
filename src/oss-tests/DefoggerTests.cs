// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using OssGadget.Tools;
using System.Reflection;
using System.Text;

public class DefoggerTests
{
    readonly string decoded = "The quick brown fox jumped over the lazy dog.";

    [Fact]
    public void DetectHex()
    {
        string? encoded = BitConverter.ToString(Encoding.Default.GetBytes(decoded)).Replace("-", "");

        DefoggerTool? tool = new();
        tool.AnalyzeFile("DetectHexTest", encoded);
        tool.Findings.Count.Should().Be(2);
        tool.Findings.Any(x => x.EncodedText == encoded && x.DecodedText == decoded).Should().BeTrue();
    }

    [Fact]
    public void DetectHexWithDash()
    {
        string? encodedWithDash = BitConverter.ToString(Encoding.Default.GetBytes(decoded));

        DefoggerTool? tool = new();
        tool.AnalyzeFile("DetectHexTest", encodedWithDash);
        tool.Findings.Should().HaveCount(1).
            And.Contain(x => x.EncodedText == encodedWithDash && x.DecodedText == decoded);
    }

    [Fact]
    public void DetectBase64()
    {
        string? base64 = Convert.ToBase64String(Encoding.Default.GetBytes(decoded));

        DefoggerTool? tool = new();
        tool.AnalyzeFile("DetectBase64Test", base64);

        tool.Findings.Should().HaveCount(1).
            And.Contain(x => x.EncodedText == base64 && x.DecodedText == decoded);
    }

    [Fact]
    public void DetectNested()
    {
        string? nested = Convert.ToHexString(Encoding.Default.GetBytes(Convert.ToBase64String(Encoding.Default.GetBytes(decoded))));

        DefoggerTool? tool = new();
        tool.AnalyzeFile("DetectNestedTest", nested);
        tool.Findings.Should().HaveCount(3);
        tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64).Should().Be(2);
        tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex).Should().Be(1);
        tool.Findings.Any(x => x.DecodedText == decoded).Should().BeTrue();
    }

    [Fact]
    public void DetectNestedZip()
    {
        FileStream? zip = new(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "Base64Zip.zip"), FileMode.Open);
        MemoryStream? ms = new();
        zip.CopyTo(ms);
        string? nested = Convert.ToHexString(Encoding.Default.GetBytes(Convert.ToBase64String(ms.ToArray())));

        DefoggerTool? tool = new();
        tool.AnalyzeFile("DetectNestedZipTest", nested);
        tool.Findings.Should().HaveCount(5);
        tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64).Should().Be(3);
        tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex).Should().Be(2);
        tool.ArchiveFindings.Should().HaveCount(1);
        tool.BinaryFindings.Should().HaveCount(1);
        tool.Findings.Any(x => x.DecodedText == decoded).Should().BeTrue();
        zip.Close();
    }

    [Fact]
    public void DetectZip()
    {
        FileStream? zip = new(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "Base64Zip.zip"), FileMode.Open);
        MemoryStream? ms = new();
        zip.CopyTo(ms);
        string? base64 = Convert.ToBase64String(ms.ToArray());

        DefoggerTool? tool = new();
        tool.AnalyzeFile("DetectZipTest", base64);
        tool.Findings.Should().HaveCount(3);
        tool.Findings.Count(x => x.DecodedText.Equals(decoded)).Should().Be(2);
        tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Base64).Should().Be(2);
        tool.Findings.Count(x => x.Type == DefoggerTool.EncodedStringType.Hex).Should().Be(2);
        tool.ArchiveFindings.Should().HaveCount(1);
        tool.BinaryFindings.Should().HaveCount(1);
        zip.Close();
    }

    [Fact]
    public void DetectBinaryTest()
    {
        FileStream? bin = new(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "oss-defog.dll"), FileMode.Open);
        MemoryStream? ms = new();
        bin.CopyTo(ms);
        string? base64 = Convert.ToBase64String(ms.ToArray());

        DefoggerTool? tool = new();
        tool.AnalyzeFile("DetectBinaryTest", base64);
        tool.BinaryFindings.Should().HaveCount(1);
    }
}
