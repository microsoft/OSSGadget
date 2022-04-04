// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Metadata;

using Contracts;

/// <summary>
/// A class to represent Package Metadata for a NuGet package version.
/// </summary>
public record CargoPackageVersionMetadata : IManagerPackageVersionMetadata
{
    public string Name { get; }
    public string Version { get; }
} 