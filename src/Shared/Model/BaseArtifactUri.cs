// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using System;

public abstract record BaseArtifactUri(Enum Type, Uri Uri, string Extension)
{
    /// <summary>
    /// The enum representing the artifact type for the project manager associated with this artifact.
    /// </summary>
    public Enum Type { get; } = Type;

    /// <summary>
    /// The <see cref="Uri"/> for where this artifact can be found online.
    /// </summary>
    public Uri Uri { get; } = Uri;

    /// <summary>
    /// The file extension for this artifact file.
    /// </summary>
    public string Extension { get; } = Extension;
}