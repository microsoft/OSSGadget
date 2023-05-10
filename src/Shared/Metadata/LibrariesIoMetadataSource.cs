// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource;

using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;

public class LibrariesIoMetadataSource : BaseMetadataSource
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
    public string ENV_LIBRARIES_IO_ENDPOINT { get; set; }= "https://libraries.io/api";
    public string? ENV_LIBRARIES_IO_API_KEY { get; set; }= null;

    // Reload periodically from https://libraries.io/api/platforms
    // curl https://libraries.io/api/platforms | jq '.[].name' | sed 's/[A-Z]/\L&/g' | sed 's/$/,/g' | sort | sed '$ s/.$//'
    public static readonly List<string> VALID_TYPES = new List<string>() {
        "alcatraz",
        "bower",
        "cargo",
        "carthage",
        "clojars",
        "cocoapods",
        "conda",
        "cpan",
        "cran",
        "dub",
        "elm",
        "go",
        "hackage",
        "haxelib",
        "hex",
        "homebrew",
        "inqlude",
        "julia",
        "maven",
        "meteor",
        "nimble",
        "npm",
        "nuget",
        "packagist",
        "pub",
        "puppet",
        "purescript",
        "pypi",
        "racket",
        "rubygems",
        "swiftpm"
    };

    public override async Task<JsonDocument?> GetMetadataAsync(string packageType, string packageNamespace, string packageName, string packageVersion, bool useCache = false)
    {
        var packageTypeEnc = string.Equals(packageType, "golang") ? "go" : packageType;
        if (!VALID_TYPES.Contains(packageTypeEnc, StringComparer.InvariantCultureIgnoreCase))
        {
            Logger.Warn("Unable to get metadata for [{} {}]. Package type [{}] is not supported. Try another data provider.", packageNamespace, packageName, packageType);
        }

        var apiKey = ENV_LIBRARIES_IO_API_KEY != null ? $"apiKey={ENV_LIBRARIES_IO_API_KEY}" : "";
        var packageNamespaceEnc = packageNamespace?.Replace("@", "%40").Replace("/", "%2F");
        var packageNameEnc = packageName.Replace("@", "%40").Replace("/", "%2F");

        var fullPackageName = string.IsNullOrWhiteSpace(packageNamespaceEnc) ?
            $"{packageNameEnc}" :
            $"{packageNamespaceEnc}%2F{packageNameEnc}";
        
        var url = $"{ENV_LIBRARIES_IO_ENDPOINT}/{packageTypeEnc}/{fullPackageName}?{apiKey}";

        try
        {
            return await GetJsonWithRetry(url);
        }
        catch(Exception ex)
        {
            Logger.Warn("Error loading package: {0}", ex.Message);
        }
        return null;
    }
}