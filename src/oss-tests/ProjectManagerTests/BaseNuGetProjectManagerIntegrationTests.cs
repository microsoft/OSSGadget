// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Microsoft.CST.OpenSource;
    using Microsoft.CST.OpenSource.PackageManagers;
    using PackageUrl;
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Xunit;

    public class BaseNuGetProjectManagerIntegrationTests
    {
        private readonly IHttpClientFactory _httpClientFactory = new DefaultHttpClientFactory();

        /// <summary>
        /// Integration test to verify that the generic V2 detection works with real NuGet.org V2 API
        /// Note: Using a known package that exists on NuGet.org V2
        /// </summary>
        [Theory]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2", typeof(NuGetV2ProjectManager))]
        public async Task Create_WithRealNuGetOrgV2Package_WorksCorrectly(string purlString, Type expectedType)
        {
            // Arrange
            PackageURL packageUrl = new(purlString);

            // Act
            BaseNuGetProjectManager manager = BaseNuGetProjectManager.Create(".", _httpClientFactory, TimeSpan.FromSeconds(30), packageUrl);

            // Assert
            Assert.IsType(expectedType, manager);
            
            // Verify the manager can actually fetch metadata (proves it's working)
            bool packageExists = await manager.PackageVersionExistsAsync(packageUrl, useCache: false);
            Assert.True(packageExists, $"Package {packageUrl} should exist but was not found by {manager.GetType().Name}");
        }

        /// <summary>
        /// Integration test to verify that PowerShell Gallery V2 continues to work (backwards compatibility)
        /// </summary>
        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.0.0?repository_url=https://www.powershellgallery.com/api/v2", typeof(NuGetV2ProjectManager))]
        public async Task Create_WithRealPowerShellGalleryPackage_WorksCorrectly(string purlString, Type expectedType)
        {
            // Arrange
            PackageURL packageUrl = new(purlString);

            // Act
            BaseNuGetProjectManager manager = BaseNuGetProjectManager.Create(".", _httpClientFactory, TimeSpan.FromSeconds(30), packageUrl);

            // Assert
            Assert.IsType(expectedType, manager);
            
            // Verify the manager can actually fetch metadata (proves it's working)
            bool packageExists = await manager.PackageVersionExistsAsync(packageUrl, useCache: false);
            Assert.True(packageExists, $"Package {packageUrl} should exist but was not found by {manager.GetType().Name}");
        }

        /// <summary>
        /// Integration test to verify that NuGet V3 APIs continue to work correctly
        /// </summary>
        [Theory]
        [InlineData("pkg:nuget/Newtonsoft.Json@13.0.1", typeof(NuGetProjectManager))]
        public async Task Create_WithRealNuGetV3Package_WorksCorrectly(string purlString, Type expectedType)
        {
            // Arrange
            PackageURL packageUrl = new(purlString);

            // Act
            BaseNuGetProjectManager manager = BaseNuGetProjectManager.Create(".", _httpClientFactory, TimeSpan.FromSeconds(30), packageUrl);

            // Assert
            Assert.IsType(expectedType, manager);
            
            // Verify the manager can actually fetch metadata (proves it's working)
            bool packageExists = await manager.PackageVersionExistsAsync(packageUrl, useCache: false);
            Assert.True(packageExists, $"Package {packageUrl} should exist but was not found by {manager.GetType().Name}");
        }
    }
}
