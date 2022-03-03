// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    using Extensions;
    using PackageUrl;

    [TestClass]
    public class PackageUrlExtensionsTests
    {
        public PackageUrlExtensionsTests()
        {
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "")]
        // [DataRow("pkg:npm/lodash@4.17.15")]
        public async Task ToStringFilenameSucceeds(string packageUrlString, string filename)
        {
            PackageURL packageUrl = new(packageUrlString);
            Assert.AreEqual(filename, packageUrl.ToStringFilename());
        }
    }
}
