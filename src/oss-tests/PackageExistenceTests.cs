// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Contracts;
using FluentAssertions;
using Model.Enums;
using Model.PackageExistence;
using System.Collections.Generic;
using VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PackageExistenceTests
{
    /// <summary>
    /// PackageRemoved should inherit type PackageNotFound as well.
    /// </summary>
    [TestMethod]
    public void PackageExistence_Removed_Is_NotFound()
    {
        IPackageExistence packageExistenceRemoved = new PackageRemoved(new HashSet<PackageRemovalReason>(new[]
        {
            PackageRemovalReason.PackageUnpublished
        }));

        packageExistenceRemoved.Should().BeAssignableTo<PackageNotFound>()
            .Subject.Exists.Should().Be(false);

        packageExistenceRemoved.Exists.Should().Be(false);
        
        if (packageExistenceRemoved is not PackageNotFound)
        {
            Assert.Fail();
        }
    }
    
    /// <summary>
    /// PackageVersionRemoved should inherit type PackageVersionNotFound as well.
    /// </summary>
    [TestMethod]
    public void PackageVersionExistence_Removed_Is_NotFound()
    {
        IPackageExistence packageVersionExistenceRemoved = new PackageVersionRemoved(new HashSet<PackageVersionRemovalReason>(new[]
        {
            PackageVersionRemovalReason.VersionUnpublished
        }));

        packageVersionExistenceRemoved.Should().BeAssignableTo<PackageVersionNotFound>()
            .Subject.Exists.Should().Be(false);

        packageVersionExistenceRemoved.Exists.Should().Be(false);

        if (packageVersionExistenceRemoved is not PackageVersionNotFound)
        {
            Assert.Fail();
        }
    }
    
    [TestMethod]
    public void PackageExistence_NotFound()
    {
        IPackageExistence packageExistenceRemoved = new PackageNotFound();

        packageExistenceRemoved.Exists.Should().Be(false);
    }
}