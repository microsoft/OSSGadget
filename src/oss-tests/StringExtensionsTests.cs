// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using OpenSource.Helpers;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("lodash-js", "-js", "", "lodash")]
    [InlineData("examplenode", "node", "", "example")]
    [InlineData("reactjs-javascript", "-javascript", "", "reactjs")]
    [InlineData("reactjs-js", "-js", "", "reactjs")]
    public void TestReplaceAtEnd(string original, string oldValue, string newValue, string expected)
    {
        Assert.Equal(expected, original.ReplaceAtEnd(oldValue, newValue));
    }
    
    [Theory]
    [InlineData("js-lodash", "js-", "", "lodash")]
    [InlineData("node-example", "node-", "", "example")]
    [InlineData("jsrxjs", "js", "", "rxjs")]
    public void TestReplaceAtStart(string original, string oldValue, string newValue, string expected)
    {
        Assert.Equal(expected, original.ReplaceAtStart(oldValue, newValue));
    }
}