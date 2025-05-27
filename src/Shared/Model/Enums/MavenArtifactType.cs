// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.ComponentModel;

namespace Microsoft.CST.OpenSource.Model.Enums;

/// <summary>
/// Maven upstream repositories supported by OSSGadget.
/// </summary>
public enum MavenArtifactType
{
    [Description(".aar")]
    Aar,

    [Description("-client.jar")]
    ClientJar,

    [Description(".ear")]
    Ear,

    [Description("-javadoc.jar")]
    JavadocJar,

    [Description(".pom")]
    Pom,

    [Description(".rar")]
    Rar,

    [Description("-sources.jar")]
    SourcesJar,

    [Description("-tests.jar")]
    TestsJar,

    [Description("-tests-sources.jar")]
    TestSourcesJar,

    [Description(".war")]
    War,

    [Description(".jar")]
    Jar,

    Unknown,
}

/// <summary>
/// Extension methods for <see cref="MavenArtifactType"/>.
/// </summary>
public static class MavenArtifactTypeExtensions
{
    public static string GetTypeNameExtension(this MavenArtifactType type)
    {
        var fieldInfo = type.GetType().GetField(type.ToString());
        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

        return attributes.Length > 0 ? attributes[0].Description : string.Empty;
    }
}