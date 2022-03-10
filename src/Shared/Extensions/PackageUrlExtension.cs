// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Extensions;

using Helpers;
using System.IO;
using System.Text.RegularExpressions;
using PackageUrl;
using System;

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
    /// pkg:npm/lodash -> lodash
    /// pkg:npm/angular/core -> @angular/core
    /// pkg:nuget/newtonsoft.json -> newtonsoft.json
    /// </example>
    /// <remarks>
    /// The full name response isn't compatible with putting this name back into a <see cref="PackageURL"/>
    /// as it contains the namespace if there is one.
    /// </remarks>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get the full name for.</param>
    /// <returns>The full name.</returns>
    public static string GetFullName(this PackageURL packageUrl)
    {
        if (!packageUrl.HasNamespace())
        {
            return packageUrl.Name;
        }

        // The full name for scoped npm packages should have an '@' at the beginning.
        string? namespaceStr = packageUrl.Type.Equals("npm", StringComparison.OrdinalIgnoreCase)
            ? $"@{packageUrl.Namespace}"
            : packageUrl.Namespace;
        return $"{namespaceStr}/{packageUrl.Name}";

    }
}