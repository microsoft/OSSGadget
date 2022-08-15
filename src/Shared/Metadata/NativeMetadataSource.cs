// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource;

using System;
using System.Threading.Tasks;
using System.Text.Json;
using PackageUrl;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CST.OpenSource.PackageManagers;
using System.Reflection;

public class NativeMetadataSource : BaseMetadataSource
{
    public static readonly List<string> VALID_TYPES = new List<string>();

    static NativeMetadataSource()
    {
        // Dynamically gather the list of valid types based on whether subtypes of
        // BaseProjectManager implement GetMetadataAsync.
        var projectManagers = typeof(BaseMetadataSource).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(BaseProjectManager)));

        foreach (var projectManager in projectManagers.Where(d => d != null))
        {
            MethodInfo? method = projectManager.GetMethod("GetMetadataAsync");
            if (method != null)
            {
                var type = projectManager.GetField("Type", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                if (type != null)
                {
                    VALID_TYPES.Add(type);
                }
            }
        }
        VALID_TYPES.Sort();
    }

    public override async Task<JsonDocument?> GetMetadataAsync(string packageType, string packageNamespace, string packageName, string packageVersion, bool useCache = false)
    {
        var packageUrl = new PackageURL(packageType, packageNamespace, packageName, packageVersion, null, null);

        var packageManager = ProjectManagerFactory.ConstructPackageManager(packageUrl);
        if (packageManager != null)
        {
            try
            {
                var metadata = await packageManager.GetMetadataAsync(packageUrl);
                if (metadata != null)
                {
                    try
                    {
                        return JsonDocument.Parse(metadata);
                    }
                    catch(Exception ex)
                    {
                        Logger.Warn(ex, "Error parsing metadata: {0}", ex.Message);
                        return JsonSerializer.SerializeToDocument(new Dictionary<string, string>() {
                            { "content", metadata }
                        });
                    }
                }
            }
            catch(Exception ex)
            {
                Logger.Warn(ex, "Error retrieving metadata: {0}", ex.Message);
            }
        }
        return null;
    }
}