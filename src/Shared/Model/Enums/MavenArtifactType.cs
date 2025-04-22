// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;

namespace Microsoft.CST.OpenSource.Model.Enums;

/// <summary>
/// Maven upstream repositories supported by OSSGadget.
/// </summary>
public enum MavenArtifactType
{
    Aar,
    ClientJar,
    Ear,
    JavadocJar,
    Pom,
    Rar,
    SourcesJar,
    TestsJar,
    TestSourcesJar,
    War,
    Jar,
    Unknown,
}

/// <summary>
/// Extension methods for <see cref="MavenArtifactType"/>.
/// </summary>
public static class MavenArtifactTypeExtensions
{
    public static string GetTypeNameExtension(this MavenArtifactType type) => type switch
    {
        MavenArtifactType.Aar => ".aar",
        MavenArtifactType.ClientJar => "-client.jar",
        MavenArtifactType.Ear => ".ear",
        MavenArtifactType.Jar => ".jar",
        MavenArtifactType.JavadocJar => "-javadoc.jar",
        MavenArtifactType.Pom => ".pom",
        MavenArtifactType.Rar => ".rar",
        MavenArtifactType.SourcesJar => "-sources.jar",
        MavenArtifactType.TestsJar => "-tests.jar",
        MavenArtifactType.TestSourcesJar => "-tests-sources.jar",
        MavenArtifactType.War => ".war",
        _ => string.Empty,
    };
}