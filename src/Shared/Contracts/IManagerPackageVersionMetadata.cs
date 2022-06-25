// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

/// <summary>
/// An interface representing the metadata returned from a project manager's API for a package version's metadata, won't contain extra or calculated properties.
/// </summary>
public interface IManagerPackageVersionMetadata
{
    /// <summary>
    /// The name of the package.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The version of the package.
    /// </summary>
    string Version { get; }
}