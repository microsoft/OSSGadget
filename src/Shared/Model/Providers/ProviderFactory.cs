// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using PackageUrl;
using System;
using System.Collections.Generic;
using System.Linq;

public static class ProviderFactory
{
    /// <summary>
    /// Get an appropriate implementation of <see cref="BaseProvider"/> for the project manager in the provided
    /// <see cref="PackageURL"/>. If no implementation of <see cref="BaseProvider"/> exists for this manager, returns null.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> for the package to create the provider for.</param>
    /// <returns>An implementation of <see cref="BaseProvider"/> for this project manager, or null.</returns>
    public static BaseProvider? CreateProvider(PackageURL purl)
    {
        if (ManagerProviders.Count == 0)
        {
            ManagerProviders.AddRange(typeof(BaseProvider).Assembly.GetTypes()
                .Where(type => type.IsSubclassOf(typeof(BaseProvider))));
        }

        // Use reflection to find the correct provider class
        Type? providerClass = ManagerProviders
            .FirstOrDefault(type => type.Name.Equals($"{purl.Type}Provider",
                StringComparison.InvariantCultureIgnoreCase));

        if (providerClass == null)
        {
            return null;
        }

        BaseProvider? provider = Activator.CreateInstance(providerClass) as BaseProvider;

        return provider;

    }

    // do reflection only once
    private static readonly List<Type> ManagerProviders = new();
}