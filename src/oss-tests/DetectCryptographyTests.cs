// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using ApplicationInspector.RulesEngine;
using OssGadget.Tools;
using PackageUrl;

public class DetectCryptographyTests
{
    [Theory]
    [InlineData("pkg:npm/blake2", "Cryptography.Implementation.Hash.Blake", "Cryptography.Implementation.Hash.Blake2", "Cryptography.Implementation.Hash.JH", "Cryptography.Implementation.Hash.SHA-512")]
    [InlineData("pkg:cargo/blake3", "Cryptography.Implementation.Hash.Blake3", "Cryptography.Implementation.Hash.SHA-512")]
    [InlineData("pkg:cargo/md2", "Cryptography.Implementation.Hash.MD2")]
    [InlineData("pkg:cargo/md4", "Cryptography.Implementation.Hash.MD4", "Cryptography.Implementation.Hash.SHA-1")]
    [InlineData("pkg:npm/md5", "Cryptography.Implementation.Hash.MD5")]
    [InlineData("pkg:cargo/md5", "Cryptography.Implementation.Hash.MD5")]
    [InlineData("pkg:npm/aes-js", "Cryptography.Implementation.BlockCipher.AES")]
    [InlineData("pkg:npm/des", "Cryptography.Implementation.BlockCipher.DES")]
    [InlineData("pkg:npm/sm4-demo", "Cryptography.Implementation.BlockCipher.SM4")]
    public async Task TestPackageDetectionSucceeds(string purl, params string[] expectedTags)
    {
        await TestDetectCryptography(purl, expectedTags);
    }

    [Fact]
    public void TestDetectCryptographyRulesValid()
    {
        DetectCryptographyTool detectCryptographyTool = new();
        detectCryptographyTool.Should().NotBeNull();
        RuleSet rules = detectCryptographyTool.GetEmbeddedRules();
        RulesVerifier analyzer = new(new RulesVerifierOptions() { DisableRequireUniqueIds = true });
        RulesVerifierResult issues = analyzer.Verify(rules);
        issues.Verified.Should().BeTrue();
        var appInspectorRules = issues.CompiledRuleSet.GetAppInspectorRules();
        appInspectorRules.Should().HaveCountGreaterThan(0);
    }

    private static async Task TestDetectCryptography(string purl, params string[] expectedTags)
    {
        DetectCryptographyTool? detectCryptographyTool = new();
        string targetDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        List<IssueRecord>? results = await detectCryptographyTool.AnalyzePackage(new PackageURL(purl), targetDirectoryName, false);

        IEnumerable<string>? distinctTargets = expectedTags.Distinct();
        IEnumerable<string>? distinctFindings = results.SelectMany(s => s.Issue.Rule.Tags ?? Array.Empty<string>())
                                      .Where(s => s.StartsWith("Cryptography.Implementation"))
                                      .Distinct();

        distinctTargets.Except(distinctFindings).Should().BeEmpty();
        distinctFindings.Except(distinctTargets).Should().BeEmpty();
    }
}
