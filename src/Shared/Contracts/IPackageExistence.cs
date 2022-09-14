// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using Model.Enums;
using System.Collections.Generic;

/// <summary>
/// An interface representing the existence of a package/version.
/// </summary>
public interface IPackageExistence
{
    /// <summary>
    /// Gets if the package/version currently exists.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Gets if the package/version had existed at one point.
    /// </summary>
    bool HasEverExisted { get; }

    /// <summary>
    /// Gets if the package/version was removed.
    /// So if it doesn't exist now, but at one point did exist.
    /// </summary>
    bool WasRemoved => !this.Exists && this.HasEverExisted;
}