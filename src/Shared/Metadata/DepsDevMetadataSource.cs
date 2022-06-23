// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource;

using RecursiveExtractor;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using PackageUrl;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CST.OpenSource.PackageManagers;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

public class DepsDevMetadataSource : BaseMetadataSource
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
    public static string ENV_DEPS_DEV_ENDPOINT = "https://deps.dev/_";

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

        var packageNamespaceEnc = ("/" + packageNamespace?.Replace("@", "%40").Replace("/", "%2F")) ?? "";
        var packageNameEnc = packageName.Replace("@", "%40").Replace("/", "%2F");
        
        // The missing slash in the next line is not a bug.
        var url = $"{ENV_DEPS_DEV_ENDPOINT}/s/{packageTypeEnc}/p{packageNamespaceEnc}/{packageNameEnc}/v/{packageVersion}";
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