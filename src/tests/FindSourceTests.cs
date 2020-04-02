using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CST.OpenSource;
using Microsoft.CST.OpenSource.Shared;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class FindSourceTests
    {
        [DataTestMethod]
        [DataRow("pkg:npm/md5", "pkg:github/pvorb/node-md5")]
        public async Task FindSource_Success(string purl, string targetResult)
        {
            FindSourceTool tool = new FindSourceTool();
            var results = await tool.FindSource(new PackageURL(purl));
            var targetPurl = new PackageURL(targetResult);
            var success = false;

            foreach (var resultPurl in results)
            {
                if (resultPurl.Equals(targetPurl))
                {
                    success = true;
                }
            }
            Assert.IsTrue(success, $"Result {targetResult} not found from {purl}");
        }

        [DataTestMethod]
        [DataRow("pkg:npm/hjkfashfkjafhakfjsa", "pkg:github/pvorb/node-md5")]
        public async Task FindSource_NonExistentPackage(string purl, string targetResult)
        {
            FindSourceTool tool = new FindSourceTool();
            var results = await tool.FindSource(new PackageURL(purl));
            Assert.IsTrue(results.Count() == 0, $"Result {results} obtained from non-existent {purl}");
        }
    }
}
