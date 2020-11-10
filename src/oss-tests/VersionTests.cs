// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class VersionTests
    {
        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            CommonInitialization.Initialize();
        }

        [DataTestMethod]
        [DataRow("0.1,0.2,0.3", "0.1,0.2,0.3")]
        [DataRow("0.1,0.3,0.2", "0.1,0.2,0.3")]
        [DataRow("0.04,0.03,0.02", "0.02,0.03,0.04")]
        [DataRow("1,23,99,0,0", "0,0,1,23,99")]
        [DataRow("1.0.1pre-1,1.0.1pre-2, 1.2,1.0", "1.0,1.0.1pre-1,1.0.1pre-2,1.2")]
        public async Task TestVersionSort(string preSortS, string postSortS)
        {
            var preSort = preSortS.Split(new[] { ',' });
            var postSort = postSortS.Split(new[] { ',' });
            var result = BaseProjectManager.SortVersions(preSort);
            Assert.IsTrue(result.SequenceEqual(postSort), $"Result {string.Join(',', result)} != {string.Join(',', postSort)}");
        }
    }
}