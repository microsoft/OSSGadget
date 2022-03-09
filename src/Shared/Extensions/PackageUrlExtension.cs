// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Extensions;

using Helpers;
using System.IO;
using System.Text.RegularExpressions;
using PackageUrl;
using System.Net;
using System.Web;

public static class PackageUrlExtension
{
    public static string ToStringFilename(this PackageURL packageUrl)
    {
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        return Regex.Replace(packageUrl.ToString(), "[" + Regex.Escape(invalidChars) + "]", "-");
    }
    
    public static string GetNameWithScope(this PackageURL packageUrl)
    {
        return packageUrl.Namespace.IsNotBlank()
            ? $"@{packageUrl.Namespace}/{packageUrl.Name}"
            : packageUrl.Name;
    }
}