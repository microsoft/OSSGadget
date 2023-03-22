// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using System;

/// <summary>
/// An interface representing the metadata returned from a project manager's API for a package version's metadata, won't contain extra or calculated properties.
/// </summary>
public abstract record BasePackageVersionMetadata : BasePackageMetadata
{
    /// <summary>
    /// The package version.
    /// </summary>
    public abstract string Version { get; init; }

    /// <summary>
    /// The publisher of this package version.
    /// </summary>
    public abstract string Publisher { get; init; }
    
    /// <summary>
    /// The description of the package version.
    /// </summary>
    public abstract string Description { get; init; }
    
    /// <summary>
    /// The keywords of the package version.
    /// </summary>
    public abstract string[] Keywords { get; init; }
    
    /// <summary>
    /// The number of this version since the first version was published.
    /// First version being 0, second version being 1, etc.
    /// </summary>
    public abstract int NumberVersion { get; set; }
    
    /// <summary>
    /// The Version increase since the previous version.
    /// </summary>
    public abstract Version VersionIncrease { get; set; }
}