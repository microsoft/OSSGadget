// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Contracts;
using Microsoft.CST.OpenSource.Utilities;
using Model;
using PackageManagers;
using PackageUrl;
using System;

public class SharedTests
{

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("v1.2.3")]
    [InlineData("v123.456.abc.789")]
    [InlineData(".123")]
    [InlineData("5")]
    [InlineData("1.2.3-release1")]
    public async Task VersionParseSucceeds(string versionString)
    {
        System.Collections.Generic.List<string>? result = VersionComparer.Parse(versionString);
        Assert.Equal(string.Join("", result), versionString);
    }
    
    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15")]
    public async Task MetadataToFromJsonSucceeds(string packageUrlString)
    {
        PackageURL packageUrl = new(packageUrlString);
        IBaseProjectManager? projectManager = ProjectManagerFactory.ConstructPackageManager(packageUrl);

        if (projectManager == null)
        {
            throw new NullReferenceException("The project manager is null.");
        }

        PackageMetadata metadata = await projectManager.GetPackageMetadataAsync(packageUrl, useCache: false);
        
        Assert.Equal("lodash", metadata.Name);
        Assert.Equal("Lodash modular utilities.", metadata.Description);
        Assert.Equal("4.17.15", metadata.PackageVersion);

        string? metadataJson = metadata.ToString();
        
        Assert.Contains("Lodash modular utilities.", metadataJson);

        PackageMetadata metadataFromJson = PackageMetadata.FromJson(metadataJson) ?? throw new InvalidOperationException("Can't deserialize the metadata json.");
        
        Assert.Equal("lodash", metadataFromJson.Name);
        Assert.Equal("Lodash modular utilities.", metadataFromJson.Description);
        Assert.Equal("4.17.15", metadataFromJson.PackageVersion);
    }
}
