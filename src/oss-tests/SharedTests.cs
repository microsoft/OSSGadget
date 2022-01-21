// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class SharedTests
    {
        public SharedTests()
        {
        }

        [DataTestMethod]
        [DataRow("1.2.3")]
        [DataRow("v1.2.3")]
        [DataRow("v123.456.abc.789")]
        [DataRow(".123")]
        [DataRow("5")]
        [DataRow("1.2.3-release1")]
        public async Task VersionParseSucceeds(string versionString)
        {
            System.Collections.Generic.List<string>? result = VersionComparer.Parse(versionString);
            Assert.AreEqual(string.Join("", result), versionString);
        }
    }
}
