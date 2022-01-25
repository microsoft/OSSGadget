// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.PackageManagers;
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
        [DataTestMethod]
        [DataRow("0.1,0.2,0.3", "0.3,0.2,0.1")]
        [DataRow("0.1,0.3,0.2", "0.3,0.2,0.1")]
        [DataRow("0.04,0.03,0.02", "0.04,0.03,0.02")]
        [DataRow("1,23,99,0,0", "99,23,1,0,0")]
        [DataRow("1.2.3,1.2.3.4,1.2.4", "1.2.4,1.2.3.4,1.2.3")]
        [DataRow("1.0.1pre-1,1.0.1pre-2,1.2,1.0", "1.2,1.0.1pre-2,1.0.1pre-1,1.0")]
        [DataRow("foo", "foo")]
        [DataRow("v1,v3,v2", "v3,v2,v1")]
        [DataRow("v1-rc1,v3-rc3,v2-rc2", "v3-rc3,v2-rc2,v1-rc1")]
        [DataRow("v1-rc1,v1-rc3,v1-rc2", "v1-rc3,v1-rc2,v1-rc1")]
        [DataRow("234,73", "234,73")]
        [DataRow("73,234", "234,73")]
        public async Task TestVersionSort(string preSortS, string postSortS)
        {
            string[]? preSort = preSortS.Split(new[] { ',' });
            string[]? postSort = postSortS.Split(new[] { ',' });
            System.Collections.Generic.IEnumerable<string>? result = BaseProjectManager.SortVersions(preSort);
            Assert.IsTrue(result.SequenceEqual(postSort), $"Result {string.Join(',', result)} != {string.Join(',', postSort)}");
        }
    }
}