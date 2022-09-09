// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using Enums;
using System;
using System.Collections.Generic;
using System.Linq;

public record PackageExistence
{
    public PackageExistence(bool exists, bool didExist, IEnumerable<PackageDoesNotExistReason>? doesNotExistReasons = null)
    {
        if (exists && !didExist)
        {
            throw new InvalidOperationException("The package can't have Exists as true, and DidExist as false!");
        }

        this.Exists = exists;
        this.DidExist = didExist;
        this.DoesNotExistReasons = doesNotExistReasons ?? new List<PackageDoesNotExistReason>();

        if (this.DoesNotExistReasons.Contains(PackageDoesNotExistReason.VersionUnpublished))
        {
            throw new InvalidOperationException("The package can't have VersionUnpublished as a does not exist reason!");
        }
    }

    public IEnumerable<PackageDoesNotExistReason> DoesNotExistReasons { get; init; }
    public bool DidExist { get; init; }
    public bool Exists { get; init; }
}

public record PackageVersionExistence
{
    public PackageVersionExistence(bool exists, bool didExist, bool packageExists, bool packageDidExist, IEnumerable<PackageDoesNotExistReason>? doesNotExistReasons = null)
    {
        if (exists && !didExist)
        {
            throw new InvalidOperationException("The package version can't have Exists as true, and DidExist as false!");
        }
        
        if (packageExists && !packageDidExist)
        {
            throw new InvalidOperationException("The package version can't have PackageExists as true, and PackageDidExist as false!");
        }
        
        if ((exists && !packageExists) || (didExist && !packageDidExist))
        {
            throw new InvalidOperationException("The package version can't exist or have existed, if the package doesn't or never did exist!");
        }

        this.Exists = exists;
        this.DidExist = didExist;
        this.PackageExists = packageExists;
        this.PackageDidExist = packageDidExist;
        this.DoesNotExistReasons = doesNotExistReasons ?? new List<PackageDoesNotExistReason>();
    }

    public IEnumerable<PackageDoesNotExistReason> DoesNotExistReasons { get; init; }
    public bool DidExist { get; init; }
    public bool Exists { get; init; }
    public bool PackageDidExist { get; init; }
    public bool PackageExists { get; init; }
}
