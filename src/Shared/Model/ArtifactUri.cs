// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using System;
using System.IO;

/// <summary>
/// A record to represent the type, uri, and extension for an artifact associated with a package.
/// </summary>
/// <typeparam name="T">The enum to represent the artifact type.</typeparam>
public record ArtifactUri<T> where T : Enum
{
    /// <summary>
    /// Initializes a new instance of <see cref="ArtifactUri{T}"/>.
    /// </summary>
    /// <param name="type">The type of artifact for this <see cref="ArtifactUri{T}"/>.</param>
    /// <param name="uri">The <see cref="Uri"/> this artifact can be found at.</param>
    public ArtifactUri(T type, Uri uri)
    {
        Type = type;
        Uri = uri;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ArtifactUri{T}"/>.
    /// </summary>
    /// <param name="type">The type of artifact for this <see cref="ArtifactUri{T}"/>.</param>
    /// <param name="uri">The string of the uri this artifact can be found at.</param>
    public ArtifactUri(T type, string uri) : this(type, new Uri(uri)) { }

    /// <summary>
    /// The enum representing the artifact type for the project manager associated with this artifact.
    /// </summary>
    public T Type { get; }

    /// <summary>
    /// The <see cref="Uri"/> for where this artifact can be found online.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// The file extension for this artifact file.
    /// </summary>
    public string Extension => Path.GetExtension(Uri.AbsolutePath);
}