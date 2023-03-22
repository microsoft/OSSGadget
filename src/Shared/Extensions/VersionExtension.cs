// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Extensions;

using System;

public static class VersionExtension
{
    public static Version GetVersionDifference(this Version version, Version otherVersion)
    {
        int majorVersionDifference = version.Major - otherVersion.Major;
        int minorVersionDifference = version.Minor - otherVersion.Minor;
        int buildVersionDifference = version.Build - otherVersion.Build;
        int revisionVersionDifference = version.Revision - otherVersion.Revision;
        return new Version(majorVersionDifference, minorVersionDifference, buildVersionDifference, revisionVersionDifference);
    }
}