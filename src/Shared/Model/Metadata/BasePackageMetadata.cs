// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model.Enums;

/// <summary>
/// An interface representing the metadata returned from a project manager's API for a package's metadata, won't contain extra or calculated properties.
/// </summary>
public abstract record BasePackageMetadata
{
    /// <summary>
    /// The type of the package.
    /// </summary>
    public abstract PackageType Type { get; }

    /// <summary>
    /// The name of the package.
    /// </summary>
    public abstract string Name { get; init; }

    /// <summary>
    /// The namespace of the package.
    /// </summary>
    public abstract string? Namespace { get; init; }

    /// <summary>
    /// The description of the package.
    /// </summary>
    public abstract string? PackageDescription { get; init; }
}