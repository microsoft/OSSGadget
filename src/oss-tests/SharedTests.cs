// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    using Contracts;
    using Model;
    using PackageManagers;
    using PackageUrl;
    using System;

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
        
        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15")]
        public async Task MetadataToFromJsonSucceeds(string packageUrlString)
        {
            PackageURL packageUrl = new(packageUrlString);
            IBaseProjectManager? projectManager = ProjectManagerFactory.ConstructPackageManager(packageUrl);

            if (projectManager == null)
            {
                throw new NullReferenceException("The project manager is null.");
            }

            PackageMetadata metadata = await projectManager.GetPackageMetadataAsync(packageUrl, useCache: false);
            
            Assert.AreEqual("lodash", metadata.Name);
            Assert.AreEqual("Lodash modular utilities.", metadata.Description);
            Assert.AreEqual("4.17.15", metadata.PackageVersion);

            string? metadataJson = metadata.ToString();
            
            Assert.IsTrue(metadataJson.Contains("Lodash modular utilities."));

            PackageMetadata metadataFromJson = PackageMetadata.FromJson(metadataJson) ?? throw new InvalidOperationException("Can't deserialize the metadata json.");
            
            Assert.AreEqual("lodash", metadataFromJson.Name);
            Assert.AreEqual("Lodash modular utilities.", metadataFromJson.Description);
            Assert.AreEqual("4.17.15", metadataFromJson.PackageVersion);
        }
    }
}
