// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

/// <summary>
/// An interface representing the existence of a package/version.
/// </summary>
public interface IPackageExistence
{
    /// <summary>
    /// Gets if the package/version currently exists.
    /// </summary>
    bool Exists { get; }
}