// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests
{
    using OpenSource.Helpers;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StringExtensionsTests
    {
        [DataTestMethod]
        [DataRow("lodash-js", "-js", "", "lodash")]
        [DataRow("examplenode", "node", "", "example")]
        [DataRow("reactjs-javascript", "-javascript", "", "reactjs")]
        [DataRow("reactjs-js", "-js", "", "reactjs")]
        public void TestReplaceAtEnd(string original, string oldValue, string newValue, string expected)
        {
            Assert.AreEqual(expected, original.ReplaceAtEnd(oldValue, newValue));
        }
        
        [DataTestMethod]
        [DataRow("js-lodash", "js-", "", "lodash")]
        [DataRow("node-example", "node-", "", "example")]
        [DataRow("jsrxjs", "js", "", "rxjs")]
        public void TestReplaceAtStart(string original, string oldValue, string newValue, string expected)
        {
            Assert.AreEqual(expected, original.ReplaceAtStart(oldValue, newValue));
        }
    }
}