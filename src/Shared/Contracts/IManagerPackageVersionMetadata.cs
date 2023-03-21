// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model.Enums;

/// <summary>
/// An interface representing the metadata returned from a project manager's API for a package version's metadata, won't contain extra or calculated properties.
/// </summary>
public interface IManagerPackageVersionMetadata : IManagerPackageMetadata
{
    /// <summary>
    /// The version of the package.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// The Publisher of the package.
    /// </summary>
    string Publisher { get; }
}