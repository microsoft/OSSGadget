// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource;

using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CST.OpenSource.PackageManagers;

public class DepsDevMetadataSource : BaseMetadataSource
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier",
        Justification = "Modified through reflection.")]
    public string ENV_DEPS_DEV_ENDPOINT { get; set; } = "https://deps.dev/_";

    public static readonly List<string> VALID_TYPES = new List<string>() {
        "npm",
        "go",
        "maven",
        "pypi",
        "cargo"
    };

    public override async Task<JsonDocument?> GetMetadataAsync(string packageType, string packageNamespace, string packageName, string packageVersion, bool useCache = false)
    {
        var packageTypeEnc = string.Equals(packageType, "golang") ? "go" : packageType;
        if (!VALID_TYPES.Contains(packageTypeEnc, StringComparer.InvariantCultureIgnoreCase))
        {
            Logger.Warn("Unable to get metadata for [{} {}]. Package type [{}] is not supported. Try another data provider.", packageNamespace, packageName, packageType);
        }
        var packageNamespaceEnc = packageNamespace?.Replace("@", "%40").Replace("/", "%2F");
        var packageNameEnc = packageName.Replace("@", "%40").Replace("/", "%2F");

        var fullPackageName = string.IsNullOrWhiteSpace(packageNamespaceEnc) ?
            $"{packageNameEnc}" :
            $"{packageNamespaceEnc}%2F{packageNameEnc}";

        // The missing slash in the next line is not a bug.
        var url = $"{ENV_DEPS_DEV_ENDPOINT}/s/{packageTypeEnc}/p/{fullPackageName}/v/{packageVersion}";
        try
        {
            return await BaseProjectManager.GetJsonCache(HttpClient, url, useCache);
        }
        catch(Exception ex)
        {
            Logger.Warn("Error loading package: {0}", ex.Message);
            return null;
        }
    }
}