// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Helpers;

using Contracts;
using Model.Metadata;
using Moq;
using NuGet.Packaging;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// Helper class for working with NuGet metadata in tests
/// </summary>
public static class NuGetTestHelper
{
    /// <summary>
    /// Creates a NuGetPackageVersionMetadata object for use in tests with 
    /// proper initialization of all required properties.
    /// </summary>
    public static NuGetPackageVersionMetadata CreateMetadata(
        string name, 
        string version, 
        string description = "", 
        string authors = "", 
        Uri? catalogUri = null,
        string tags = "")
    {
        // Instead of directly creating the metadata object with the problematic IEnumerable property,
        // we'll create a TestNuGetPackageVersionMetadata that uses a List instead
        return new TestNuGetPackageVersionMetadata
        {
            Name = name,
            Version = version,
            Description = description,
            Authors = authors,
            Tags = tags,
            CatalogUri = catalogUri ?? new Uri($"https://api.nuget.org/v3/catalog0/data/2022.03.11.23.17.27/{name}.{version}.json"),
            DependencySets = new List<PackageDependencyGroup>() // Empty list to avoid serialization issues
        };
    }

    /// <summary>
    /// Sets up a mock package actions object for testing NuGet package operations
    /// </summary>
    /// <returns>A mocked IManagerPackageActions that returns the specified metadata and versions</returns>
    public static IManagerPackageActions<NuGetPackageVersionMetadata> SetupPackageActions(
        PackageURL purl,
        NuGetPackageVersionMetadata metadata,
        IEnumerable<string>? versions = null)
    {
        Mock<IManagerPackageActions<NuGetPackageVersionMetadata>> mockActions = new();
        
        // Set up GetMetadataAsync to return our metadata
        mockActions.Setup(m => m.GetMetadataAsync(
                It.Is<PackageURL>(p => p.Name.Equals(purl.Name, StringComparison.OrdinalIgnoreCase) && 
                                       (purl.Version == null || p.Version == purl.Version)),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // If versions are provided, set up GetAllVersionsAsync and GetLatestVersionAsync
        if (versions != null)
        {
            string[] versionArray = versions as string[] ?? versions.ToArray();
            
            mockActions.Setup(m => m.GetAllVersionsAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name, StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(versionArray);
                
            if (versionArray.Length > 0)
            {
                mockActions.Setup(m => m.GetLatestVersionAsync(
                        It.Is<PackageURL>(p => p.Name.Equals(purl.Name, StringComparison.OrdinalIgnoreCase)),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(versionArray[0]);
            }
        }
        
        // Set up DoesPackageExistAsync to return true for this package
        mockActions.Setup(m => m.DoesPackageExistAsync(
                It.Is<PackageURL>(p => p.Name.Equals(purl.Name, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return mockActions.Object;
    }
}

/// <summary>
/// A test-specific implementation of NuGetPackageVersionMetadata that uses a List for DependencySets
/// to avoid serialization issues
/// </summary>
public record TestNuGetPackageVersionMetadata : NuGetPackageVersionMetadata
{
    // Override the problematic property with a concrete List implementation
    public new List<PackageDependencyGroup> DependencySets { get; set; } = new();
    
    // This property is accessed in some places so we need to ensure it returns our concrete List
    public IEnumerable<PackageDependencyGroup> DependencySetsInternal => DependencySets;
}
