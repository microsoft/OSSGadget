// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class FindSourceTests
    {
        [DataTestMethod]
        [DataRow("pkg:npm/md5", "pkg:github/pvorb/node-md5")]
        [DataRow("pkg:pypi/moment", "pkg:github/zachwill/moment")]
        [DataRow("pkg:nuget/Newtonsoft.Json", "pkg:github/jamesnk/newtonsoft.json")]
        [DataRow("pkg:pypi/django", "pkg:github/django/django")]
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
        [DataRow("pkg:npm/md5", "https://github.com/pvorb/node-md5")]
        public async Task Check_Sarif(string purl, string targetResult)
        {
            // for initialization
            FindSourceTool tool = new FindSourceTool();

            RepoSearch searchTool = new RepoSearch();
            var results = await searchTool.ResolvePackageLibraryAsync(new PackageURL(purl));

            List<Result> sarifResults = new List<Result>();
            foreach (var result in results)
            {
                var confidence = result.Value * 100.0;

                Result sarifResult = new Result()
                {
                    Message = new Message()
                    {
                        Text = $"https://github.com/{result.Key.Namespace}/{result.Key.Name}"
                    },
                    Kind = ResultKind.Informational,
                    Level = FailureLevel.None,
                    Rank = confidence
                };

                sarifResults.Add(sarifResult);
            }

            SarifBuilder sarifBuilder = new SarifBuilder();
            SarifLog sarif = sarifBuilder.BuildSingleRunSarifLog(sarifResults);
            Assert.IsNotNull(sarif);
            Assert.IsNotNull(sarif.Runs.FirstOrDefault().Tool.Driver.Name);
            Assert.AreEqual(sarif.Runs.FirstOrDefault().Results.FirstOrDefault().Message.Text, targetResult);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/hjkfashfkjafhakfjsa", "")]
        [DataRow("pkg:pypi/hjkfashfkjafhakfjsa", "")]
        [DataRow("pkg:nuget/hjkfashfkjafhakfjsa", "")]
        public async Task FindSource_NonExistentPackage(string purl, string _)
        {
            // for initialization
            FindSourceTool tool = new FindSourceTool();

            RepoSearch searchTool = new RepoSearch();
            var results = await searchTool.ResolvePackageLibraryAsync(new PackageURL(purl));
            Assert.IsTrue(results.Count() == 0, $"Result {results} obtained from non-existent {purl}");
        }
    }
}
