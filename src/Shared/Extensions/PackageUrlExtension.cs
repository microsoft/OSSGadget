// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Extensions;

using Helpers;
using System.IO;
using System.Text.RegularExpressions;
using PackageUrl;
using System.Net;
using System.Collections.Generic;

public static class PackageUrlExtension
{

    private const string REPOSITORY_URL_KEY = "repository_url";
    
    /// <summary>
    /// Constructs a new <see cref="PackageURL"/> with the same type as the provided one, but with a new name,
    /// and optionally a new namespace.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get the type from.</param>
    /// <param name="name">The new name.</param>
    /// <param name="namespaceStr">The new namespace if one was provided, otherwise null.</param>
    /// <returns>A new <see cref="PackageURL"/> with a new name, and a new namespace if provided.</returns>
    public static PackageURL CreateWithNewNames(this PackageURL packageUrl, string name, string? namespaceStr = null)
    {
        return new PackageURL(packageUrl.Type, namespaceStr, name, null, null, null);
    }
    
    /// <summary>
    /// Returns a new <see cref="PackageURL"/> instance with version.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/>.</param>
    /// <param name="version">The version to set.</param>
    /// <returns>Returns a new <see cref="PackageURL"/> instance with a version.</returns>
    public static PackageURL WithVersion(this PackageURL packageUrl, string version)
    {
        PackageURL purl = new PackageURL(
            type: packageUrl.Type,
            @namespace: packageUrl.Namespace,
            name: packageUrl.Name,
            version: version,
            qualifiers: packageUrl.Qualifiers,
            subpath: packageUrl.Subpath);
        return purl;
    }

    /// <summary>
    /// Gets the <paramref name="packageUrl"/> as a valid file name.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get as a valid file name.</param>
    /// <returns>A file name from the <paramref name="packageUrl"/>.</returns>
    public static string ToStringFilename(this PackageURL packageUrl)
    {
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        return Regex.Replace(packageUrl.WithoutQualifiers().ToString(), "[" + Regex.Escape(invalidChars) + "]", "-");
    }

    public static bool HasNamespace(this PackageURL packageUrl)
    {
        return packageUrl.Namespace.IsNotBlank();
    }
    
    /// <summary>
    /// We want npm package's namespace to be prefixed with "%40" the percent code of "@".
    /// </summary>
    /// <remarks>If the <paramref name="packageUrl"/> isn't npm, it returns the namespace with no changes.</remarks>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to get a formatted namespace for.</param>
    /// <returns>The formatted namespace of <paramref name="packageUrl"/>.</returns>
    public static string GetNamespaceFormatted(this PackageURL packageUrl)
    {
        if (packageUrl.Type != "npm" || packageUrl.Namespace.StartsWith("%40"))
        {
            return packageUrl.Namespace;
        }

        if (packageUrl.Namespace.StartsWith("@"))
        {
            return $"%40{packageUrl.Namespace.TrimStart('@')}";
        }

        return $"%40{packageUrl.Namespace}";
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
    /// <param name="encoded">If the name should be url encoded, defaults to false.</param>
    /// <returns>The full name.</returns>
    public static string GetFullName(this PackageURL packageUrl, bool encoded = false)
    {
        if (!packageUrl.HasNamespace())
        {
            return packageUrl.Name;
        }

        string name = $"{packageUrl.GetNamespaceFormatted()}/{packageUrl.Name}";
        // The full name for scoped npm packages should have an '@' at the beginning.
        return encoded ? name : WebUtility.UrlDecode(name);
    }
    /// <summary>
    /// Returns a new <see cref="PackageURL"/> instance without qualifiers.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/>.</param>
    /// <returns>Returns a new <see cref="PackageURL"/> instance without qualifiers.</returns>
    public static PackageURL WithoutQualifiers(this PackageURL packageUrl) => new(
        type: packageUrl.Type,
        @namespace: packageUrl.Namespace,
        name: packageUrl.Name,
        version: packageUrl.Version,
        qualifiers: null,
        subpath: packageUrl.Subpath);

    /// <summary>
    /// Attempts to retrieve the repository URL from the qualifiers of the <paramref name="packageUrl"/>.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to extract the repository URL from.</param>
    /// <param name="repositoryUrl">The decoded repository URL if found, otherwise null.</param>
    /// <returns>True if the repository URL was found, otherwise false.</returns>
    public static bool TryGetRepositoryUrl(this PackageURL packageUrl, out string? repositoryUrl)
    {
        if (packageUrl.Qualifiers?.TryGetValue(REPOSITORY_URL_KEY, out repositoryUrl) == true)
        {
            return true;
        }
        repositoryUrl = null;
        return false;
    }


    public static string? GetQualifierValueOrDefault(this PackageURL packageUrl, string key, string? defaultValue = null) => 
            packageUrl.Qualifiers?.GetValueOrDefault(key, defaultValue) ?? defaultValue;

    /// <summary>
    /// Retrieves the repository URL from the qualifiers of the <paramref name="packageUrl"/>.
    /// </summary>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to extract the repository URL from.</param>
    /// <returns>The decoded repository URL if found, otherwise null.</returns>
    public static string? GetRepositoryUrlOrDefault(this PackageURL packageUrl, string defaultValue) => 
        packageUrl.GetQualifierValueOrDefault(REPOSITORY_URL_KEY, defaultValue);

}