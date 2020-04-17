using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CST.OpenSource;
using Microsoft.CST.OpenSource.Shared;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class DetectCryptographyTests
    {
        [DataTestMethod]
        [DataRow("pkg:npm/md5", "Cryptography.Implementation.Hash.MD5")]
        [DataRow("pkg:npm/aes-js", "Cryptography.Implementation.BlockCipher.AES")]
        [DataRow("pkg:npm/des", "Cryptography.Implementation.BlockCipher.DES")]
        public async Task TestPackageDectionSucceeds(string purl, params string[] expectedTags)
        {
            await TestDetectCryptography(purl, expectedTags);
        }

        private async Task TestDetectCryptography(string purl, params string[] expectedTags)
        {
            var detectCryptographyTool = new DetectCryptographyTool();
            var results = await detectCryptographyTool.AnalyzePackage(new PackageURL(purl));

            var distinctTargets = expectedTags.Distinct();
            var distinctFindings = results.SelectMany(s => s.Issue.Rule.Tags)
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
