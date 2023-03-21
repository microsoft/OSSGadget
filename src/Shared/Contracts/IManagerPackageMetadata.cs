// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model.Enums;

/// <summary>
/// An interface representing the metadata returned from a project manager's API for a package's metadata, won't contain extra or calculated properties.
/// </summary>
public interface IManagerPackageMetadata
{
    /// <summary>
    /// The name of the package.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The type of the package.
    /// </summary>
    PackageType Type { get; }

    /// <summary>
    /// The Publisher of the package.
    /// </summary>
    string Publisher { get; }
}