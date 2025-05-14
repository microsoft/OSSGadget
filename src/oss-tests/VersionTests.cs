// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Microsoft.CST.OpenSource.PackageManagers;

public class VersionTests
{
    [Theory]
    [InlineData("0.1,0.2,0.3", "0.3,0.2,0.1")]
    [InlineData("0.1,0.3,0.2", "0.3,0.2,0.1")]
    [InlineData("0.04,0.03,0.02", "0.04,0.03,0.02")]
    [InlineData("1,23,99,0,0", "99,23,1,0,0")]
    [InlineData("1.2.3,1.2.3.4,1.2.4", "1.2.4,1.2.3.4,1.2.3")]
    [InlineData("1.0.1pre-1,1.0.1pre-2,1.2,1.0", "1.2,1.0.1pre-2,1.0.1pre-1,1.0")]
    [InlineData("foo", "foo")]
    [InlineData("v1,v3,v2", "v3,v2,v1")]
    [InlineData("v1-rc1,v3-rc3,v2-rc2", "v3-rc3,v2-rc2,v1-rc1")]
    [InlineData("v1-rc1,v1-rc3,v1-rc2", "v1-rc3,v1-rc2,v1-rc1")]
    [InlineData("234,73", "234,73")]
    [InlineData("73,234", "234,73")]
    public async Task TestVersionSort(string preSortS, string postSortS)
    {
        string[]? preSort = preSortS.Split(new[] { ',' });
        string[]? postSort = postSortS.Split(new[] { ',' });
        System.Collections.Generic.IEnumerable<string>? result = BaseProjectManager.SortVersions(preSort);
        Assert.True(result.SequenceEqual(postSort), $"Result {string.Join(',', result)} != {string.Join(',', postSort)}");
    }
}