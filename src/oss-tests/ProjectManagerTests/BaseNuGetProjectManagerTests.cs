// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource.PackageManagers;
    using Moq;
    using PackageUrl;
    using System;
    using System.Net.Http;
    using Xunit;

    public class BaseNuGetProjectManagerTests
    {
        [Theory]
        // Backwards compatibility - PowerShell Gallery V2
        [InlineData(
            "pkg:nuget/TestPackage?repository_url=https://www.powershellgallery.com/api/v2",
            typeof(NuGetV2ProjectManager))]
        // New functionality - Generic V2 APIs
        [InlineData(
            "pkg:nuget/TestPackage?repository_url=https://www.nuget.org/api/v2",
            typeof(NuGetV2ProjectManager))]
        [InlineData(
            "pkg:nuget/TestPackage?repository_url=https://private.example.com/api/v2",
            typeof(NuGetV2ProjectManager))]
        // V3 APIs continue to work
        [InlineData(
            "pkg:nuget/TestPackage?repository_url=https://api.nuget.org/v3/index.json",
            typeof(NuGetProjectManager))]
        [InlineData(
            "pkg:nuget/TestPackage?repository_url=https://example.com",
            typeof(NuGetProjectManager))]
        [InlineData(
            "pkg:nuget/TestPackage",
            typeof(NuGetProjectManager))]
        public void Create_ReturnsExpectedManagerType(string purlString, Type expectedType)
        {
            // Arrange
            Mock<IHttpClientFactory> mockHttpClientFactory = new();
            PackageURL packageUrl = new(purlString);

            // Act
            BaseNuGetProjectManager result = BaseNuGetProjectManager.Create(".", mockHttpClientFactory.Object, TimeSpan.Zero, packageUrl);

            // Assert
            Assert.IsType(expectedType, result);
        }
    }
}
