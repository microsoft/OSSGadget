// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using System;
using System.IO;

public readonly record struct ArtifactUri
{
    /// <summary>
    /// Initializes a new instance of <see cref="ArtifactUri"/>.
    /// </summary>
    /// <param name="type">The type of artifact for this <see cref="ArtifactUri"/>.</param>
    /// <param name="uri">The <see cref="Uri"/> this artifact can be found at.</param>
    /// <param name="extension">The file extension for the file found at the <paramref name="uri"/>.</param>
    public ArtifactUri(Enum type, Uri uri, string? extension = null)
    {
        Type = type;
        Uri = uri;
        Extension = extension ?? Path.GetExtension(uri.AbsolutePath);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ArtifactUri"/>.
    /// </summary>
    /// <param name="type">The type of artifact for this <see cref="ArtifactUri"/>.</param>
    /// <param name="uri">The string of the uri this artifact can be found at.</param>
    /// <param name="extension">The file extension for the file found at the <paramref name="uri"/>.</param>
    public ArtifactUri(Enum type, string uri, string? extension = null) : this(type, new Uri(uri), extension) { }

    /// <summary>
    /// The enum representing the artifact type for the project manager associated with this artifact.
    /// </summary>
    public Enum Type { get; }
    
    /// <summary>
    /// The <see cref="Uri"/> for where this artifact can be found online.
    /// </summary>
    public Uri Uri { get; }
    
    /// <summary>
    /// The file extension for this artifact file.
    /// </summary>
    public string Extension { get; }
}