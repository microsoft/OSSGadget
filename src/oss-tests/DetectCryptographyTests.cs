// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    using ApplicationInspector.RulesEngine;
    using PackageUrl;

    [TestClass]
    public class DetectCryptographyTests
    {
        [DataTestMethod]
        [DataRow("pkg:npm/blake2", "Cryptography.Implementation.Hash.Blake", "Cryptography.Implementation.Hash.Blake2", "Cryptography.Implementation.Hash.JH", "Cryptography.Implementation.Hash.SHA-512")]
        [DataRow("pkg:cargo/blake3", "Cryptography.Implementation.Hash.Blake3", "Cryptography.Implementation.Hash.SHA-512")]
        [DataRow("pkg:cargo/md2", "Cryptography.Implementation.Hash.MD2")]
        [DataRow("pkg:cargo/md4", "Cryptography.Implementation.Hash.MD4", "Cryptography.Implementation.Hash.SHA-1")]
        [DataRow("pkg:npm/md5", "Cryptography.Implementation.Hash.MD5")]
        [DataRow("pkg:cargo/md5", "Cryptography.Implementation.Hash.MD5")]
        [DataRow("pkg:npm/aes-js", "Cryptography.Implementation.BlockCipher.AES")]
        [DataRow("pkg:npm/des", "Cryptography.Implementation.BlockCipher.DES")]
        [DataRow("pkg:npm/sm4-demo", "Cryptography.Implementation.BlockCipher.SM4")]
        public async Task TestPackageDetectionSucceeds(string purl, params string[] expectedTags)
        {
            await TestDetectCryptography(purl, expectedTags);
        }

        [TestMethod]
        public void TestDetectCryptographyRulesValid()
        {
            DetectCryptographyTool detectCryptographyTool = new();
            Assert.IsNotNull(detectCryptographyTool);
            RuleSet rules = detectCryptographyTool.GetEmbeddedRules();
            RulesVerifier analyzer = new RulesVerifier(new RulesVerifierOptions() { DisableRequireUniqueIds = true });
            RulesVerifierResult issues = analyzer.Verify(rules);
            Assert.IsTrue(issues.Verified);
            Assert.AreNotEqual(0, issues.CompiledRuleSet.GetAppInspectorRules().Count());
        }

        private async Task TestDetectCryptography(string purl, params string[] expectedTags)
        {
            DetectCryptographyTool? detectCryptographyTool = new();
            string targetDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            List<IssueRecord>? results = await detectCryptographyTool.AnalyzePackage(new PackageURL(purl), targetDirectoryName, false);

            IEnumerable<string>? distinctTargets = expectedTags.Distinct();
            IEnumerable<string>? distinctFindings = results.SelectMany(s => s.Issue.Rule.Tags ?? Array.Empty<string>())
                                          .Where(s => s.StartsWith("Cryptography.Implementation"))
                                          .Distinct();

            if (distinctTargets.Except(distinctFindings).Any())
            {
                Assert.Fail("Missing findings: {0}", string.Join(", ", distinctTargets.Except(distinctFindings)));
            }
            if (distinctFindings.Except(distinctTargets).Any())
            {
                Assert.Fail("Unexpected findings: {0}", string.Join(", ", distinctFindings.Except(distinctTargets)));
            }
        }
    }
}