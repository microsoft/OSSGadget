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

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "new-name", null, "pkg:npm/new-name")]
        [DataRow("pkg:npm/angular/core@1.0.0", "new-name", "new-namespace", "pkg:npm/new-namespace/new-name")]
        public void CreateWithNewNamesSucceeds(string packageUrlString, string newName, string? newNamespace, string expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            PackageURL newPackageUrl = packageUrl.CreateWithNewNames(newName, newNamespace);
            Assert.AreEqual(expected, newPackageUrl.ToString());
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash", "1.0.0", "pkg:npm/lodash@1.0.0")]
        [DataRow("pkg:npm/lodash@4.17.15", "1.0.0", "pkg:npm/lodash@1.0.0")]
        public void WithVersionSucceeds(string packageUrlString, string version, string expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            PackageURL newPackageUrl = packageUrl.WithVersion(version);
            Assert.AreEqual(expected, newPackageUrl.ToString());
        }

        [DataTestMethod]
        [DataRow("pkg:npm/angular/core", true)]
        [DataRow("pkg:npm/lodash", false)]
        public void HasNamespaceSucceeds(string packageUrlString, bool expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            Assert.AreEqual(expected, packageUrl.HasNamespace());
        }

        [DataTestMethod]
        [DataRow("pkg:npm/angular/core", "%40angular")]
        [DataRow("pkg:nuget/newtonsoft.json", null)]
        [DataRow("pkg:github/microsoft/vscode", "microsoft")]
        [DataRow("pkg:docker/library/nginx", "library")]
        public void GetNamespaceFormattedSucceeds(string packageUrlString, string expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            Assert.AreEqual(expected, packageUrl.GetNamespaceFormatted());
        }

        [DataTestMethod]
        [DataRow("pkg:npm/angular/core", "@angular/core")]
        [DataRow("pkg:npm/lodash", "lodash")]
        public void GetFullNameSucceeds(string packageUrlString, string expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            Assert.AreEqual(expected, packageUrl.GetFullName());
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15?repository_url=https://example.com", "pkg:npm/lodash@4.17.15")]
        public void WithoutQualifiersSucceeds(string packageUrlString, string expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            PackageURL newPackageUrl = packageUrl.WithoutQualifiers();
            Assert.AreEqual(expected, newPackageUrl.ToString());
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash?repository_url=https://example.com", true, "https://example.com")]
        [DataRow("pkg:npm/lodash", false, null)]
        public void TryGetRepositoryUrlSucceeds(string packageUrlString, bool expectedResult, string? expectedUrl)
        {
            PackageURL packageUrl = new(packageUrlString);
            bool result = packageUrl.TryGetRepositoryUrl(out string? repositoryUrl);
            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedUrl, repositoryUrl);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash?key=value", "key", "default", "value")]
        [DataRow("pkg:npm/lodash", "key", "default", "default")]
        public void GetQualifierValueOrDefaultSucceeds(string packageUrlString, string key, string defaultValue, string expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            Assert.AreEqual(expected, packageUrl.GetQualifierValueOrDefault(key, defaultValue));
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash?repository_url=https://example.com", "default", "https://example.com")]
        [DataRow("pkg:npm/lodash", "default", "default")]
        public void GetRepositoryUrlOrDefaultSucceeds(string packageUrlString, string defaultValue, string expected)
        {
            PackageURL packageUrl = new(packageUrlString);
            Assert.AreEqual(expected, packageUrl.GetRepositoryUrlOrDefault(defaultValue));
        }


    }
}
