// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using Extensions;
using System;
using System.IO;

/// <summary>
/// A record to represent the type, uri, extension and optionally the upload time for an artifact associated with a package version.
/// </summary>
/// <typeparam name="T">The enum to represent the artifact type.</typeparam>
public record ArtifactUri<T> where T : Enum
{
    /// <summary>
    /// Initializes a new instance of <see cref="ArtifactUri{T}"/>.
    /// </summary>
    /// <param name="type">The type of artifact for this <see cref="ArtifactUri{T}"/>.</param>
    /// <param name="uri">The <see cref="Uri"/> this artifact can be found at.</param>
    /// <param name="uploadTime">The <see cref="DateTime"/> for when this artifact was uploaded to the repository.</param>
    public ArtifactUri(T type, Uri uri, DateTime? uploadTime = null)
    {
        Type = type;
        Uri = uri;
        UploadTime = uploadTime;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ArtifactUri{T}"/>.
    /// </summary>
    /// <param name="type">The type of artifact for this <see cref="ArtifactUri{T}"/>.</param>
    /// <param name="uri">The string of the uri this artifact can be found at.</param>
    public ArtifactUri(T type, string uri, DateTime? uploadTime = null) : this(type, new Uri(uri), uploadTime) { }

    /// <summary>
    /// The enum representing the artifact type for the project manager associated with this artifact.
    /// </summary>
    public T Type { get; }

    /// <summary>
    /// The <see cref="Uri"/> for where this artifact can be found online.
    /// </summary>
    public Uri Uri { get; }
    
    /// <summary>
    /// The <see cref="DateTime"/> for when this artifact was uploaded to the repository.
    /// </summary>
    public DateTime? UploadTime { get; }

    /// <summary>
    /// The file extension for this artifact file. Including the '.' at the beginning.
    /// </summary>
    /// <remarks>If the file has no extension, it will just be an empty string.</remarks>
    public string Extension => Uri.GetExtension();
}
