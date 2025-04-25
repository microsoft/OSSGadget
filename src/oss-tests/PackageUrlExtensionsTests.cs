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
        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "pkg-npm-lodash@4.17.15")]
        [DataRow("pkg:nuget/newtonsoft.json", "pkg-nuget-newtonsoft.json")]
        [DataRow("pkg:nuget/PSReadLine@2.2.0?repository_url=https://www.powershellgallery.com/api/v2", "pkg-nuget-PSReadLine@2.2.0")]
        public void ToStringFilenameSucceeds(string packageUrlString, string filename)
        {
            PackageURL packageUrl = new(packageUrlString);
            Assert.AreEqual(filename, packageUrl.ToStringFilename());
        }
    }
}
