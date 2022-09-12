// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.PackageExistence;

using Contracts;
using Enums;
using System.Collections.Generic;

/// <summary>
/// Represents a package version that currently exists.
/// </summary>
public record PackageVersionExists : IPackageExistence
{
    public bool Exists => true;
    public bool HasEverExisted => true;
    public IReadOnlySet<PackageRemovalReason>? RemovalReasons => null;
}

/// <summary>
/// Represents a package version that never existed.
/// </summary>
public record PackageVersionNotFound : IPackageExistence
{
    public bool Exists => false;
    public bool HasEverExisted => false;
    public IReadOnlySet<PackageRemovalReason>? RemovalReasons => null;
}

/// <summary>
/// Represents a package version that was removed, and why.
/// </summary>
public record PackageVersionRemoved(IReadOnlySet<PackageRemovalReason> RemovalReasons) : IPackageExistence
{
    public bool Exists => false;
    public bool HasEverExisted => true;
}