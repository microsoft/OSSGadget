// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using System;
using System.IO;

public struct ArtifactUri
{
    public ArtifactUri(ArtifactType type, Uri uri, string? extension = null)
    {
        Type = type;
        Uri = uri;
        Extension = extension ?? Path.GetExtension(uri.AbsolutePath);
    }

    public ArtifactUri(ArtifactType type, string uri, string? extension = null)
    {
        Type = type;
        Uri = new Uri(uri);
        Extension = extension ?? Path.GetExtension(uri);
    }
    
    public ArtifactType Type { get; }
    public Uri Uri { get; }
    public string Extension { get; }

    public enum ArtifactType
    {
        Unknown = 0,
        Tarball,
        ZipFile,
        Binary,

        // NuGet specific
        Nupkg,
        Nuspec,

        // PyPI specific
        Wheel,

        // Maven specific
        Pom,
        Jar,
        Sources,
    }
}