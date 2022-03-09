// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Extensions;

using Helpers;
using System.IO;
using System.Text.RegularExpressions;
using PackageUrl;

public static class PackageUrlExtension
{
    public static string ToStringFilename(this PackageURL packageUrl)
    {
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        return Regex.Replace(packageUrl.ToString(), "[" + Regex.Escape(invalidChars) + "]", "-");
    }

    public static bool HasNamespace(this PackageURL packageUrl)
    {
        return packageUrl.Namespace.IsNotBlank();
    }
    
    /// <summary>
    /// Gets the package's full name including namespace if applicable.
    /// </summary>
    /// <example>
    /// lodash -> lodash
    /// @angular/core -> angular/core
    /// </example>
    /// <remarks>Doesn't contain any prefix to the namespace, so no @ for scoped npm packages for example.</remarks>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get the full name for.</param>
    /// <returns>The full name.</returns>
    public static string GetFullName(this PackageURL packageUrl)
    {
        return packageUrl.HasNamespace() ? $"{packageUrl.Namespace}/{packageUrl.Name}" : packageUrl.Name;
    }
}