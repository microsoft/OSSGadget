// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource.PackageManagers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PackageUrl;
    using System;
    using System.Net.Http;

    [TestClass]
    public class BaseNuGetProjectManagerTests
    {
        [DataTestMethod]
        [DataRow(
            "pkg:nuget/TestPackage?repository_url=https://www.powershellgallery.com/api/v2",
            typeof(NuGetV2ProjectManager),
            DisplayName = "PowerShellGallery - Returns NuGetV2ProjectManager")]
        [DataRow(
            "pkg:nuget/TestPackage?repository_url=https://api.nuget.org/v3/index.json",
            typeof(NuGetProjectManager),
            DisplayName = "NuGetV3 - Returns NuGetProjectManager")]
        [DataRow(
            "pkg:nuget/TestPackage?repository_url=https://example.com",
            typeof(NuGetProjectManager),
            DisplayName = "CustomRepository - Returns NuGetProjectManager")]
        [DataRow(
            "pkg:nuget/TestPackage",
            typeof(NuGetProjectManager),
            DisplayName = "NoRepositoryUrl - Defaults to NuGetProjectManager")]

        public void Create_ReturnsExpectedManagerType(string purlString, Type expectedType)
        {
            // Arrange
            Mock<IHttpClientFactory> mockHttpClientFactory = new();
            PackageURL packageUrl = new(purlString);

            // Act
            BaseNuGetProjectManager result = BaseNuGetProjectManager.Create(".", mockHttpClientFactory.Object, TimeSpan.Zero, packageUrl);

            // Assert
            Assert.IsInstanceOfType(result, expectedType);
        }
    }
        
}
