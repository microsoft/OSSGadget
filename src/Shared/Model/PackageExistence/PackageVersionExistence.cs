// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.PackageExistence;

using Contracts;
using Enums;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Represents a package version that currently exists.
/// </summary>
public record PackageVersionExists : IPackageExistence
{
    public bool Exists => true;
}

/// <summary>
/// Represents a package version that never existed.
/// </summary>
public record PackageVersionNotFound : IPackageExistence
{
    public bool Exists => false;
}

/// <summary>
/// Represents a package version that was removed, and why.
/// </summary>
/// <param name="RemovalReasons">The reasons (if any) for why a package was removed.</param>
public record PackageVersionRemoved(IReadOnlySet<PackageVersionRemovalReason> RemovalReasons) : PackageVersionNotFound
{
    protected override bool PrintMembers(StringBuilder stringBuilder)
    {
        if (base.PrintMembers(stringBuilder))
        {
            stringBuilder.Append(", ");
        }

        string reasons = string.Join(',', this.RemovalReasons.Select(r => r.ToString()));

        stringBuilder.Append($"RemovalReasons = [{reasons}]");
        return true;
    }
}