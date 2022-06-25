// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Extensions;

using System;
using System.IO;

public static class UriExtension
{
    private static readonly string[] SpecialExtensions = { ".tar.gz", ".tar.bz2" };

    /// <summary>
    /// Gets the extension from a <see cref="Uri.AbsolutePath"/>.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to get the extension from.</param>
    /// <returns>The extension from the <paramref name="uri"/>, or an empty string if it doesn't have one.</returns>
    public static string GetExtension(this Uri uri)
    {
        string absolutePath = uri.AbsolutePath;

        foreach (string specialExtension in SpecialExtensions)
        {
            if (absolutePath.EndsWith(specialExtension))
            {
                return specialExtension;
            }
        }

        return Path.GetExtension(absolutePath);
    }
}