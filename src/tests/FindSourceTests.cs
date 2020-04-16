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
        [DataRow("pkg:pypi/moment", "pkg:github/zachwill/moment")]
        [DataRow("pkg:nuget/Newtonsoft.Json", "pkg:github/jamesnk/newtonsoft.json")]
        public async Task FindSource_Success(string purl, string targetResult)
        {
            // for initialization
            FindSourceTool tool = new FindSourceTool();

            RepoSearch searchTool = new RepoSearch();
            var results = await searchTool.ResolvePackageLibraryAsync(new PackageURL(purl));
            var targetPurl = new PackageURL(targetResult);
            var success = false;

            foreach (var resultEntry in results)
            {
                if (resultEntry.Key.Equals(targetPurl))
                {
                    success = true;
                }
            }
            Assert.IsTrue(success, $"Result {targetResult} not found from {purl}");
        }

        [DataTestMethod]
        [DataRow("pkg:npm/hjkfashfkjafhakfjsa", "pkg:github/pvorb/node-md5")]
        [DataRow("pkg:pypi/hjkfashfkjafhakfjsa", "pkg:github/pvorb/node-md5")]
        [DataRow("pkg:nuget/hjkfashfkjafhakfjsa", "pkg:github/pvorb/node-md5")]
        public async Task FindSource_NonExistentPackage(string purl, string targetResult)
        {
            // for initialization
            FindSourceTool tool = new FindSourceTool();

            RepoSearch searchTool = new RepoSearch();
            var results = await searchTool.ResolvePackageLibraryAsync(new PackageURL(purl));
            Assert.IsTrue(results.Count() == 0, $"Result {results} obtained from non-existent {purl}");
        }
    }
}
