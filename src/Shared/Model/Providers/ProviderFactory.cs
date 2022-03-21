// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using Contracts;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

public static class ProviderFactory
{
    /// <summary>
    /// Create a <see cref="BaseProvider"/>.
    /// </summary>
    /// <returns>A new <see cref="BaseProvider"/>.</returns>
    public static BaseProvider CreateBaseProvider()
    {
        return new BaseProvider();
    }

    /// <summary>
    /// Get an appropriate project manager for package given its PackageURL.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> for the package to create the project manager for.</param>
    /// <param name="httpClientFactory"> The <see cref="IHttpClientFactory"/> for the project manager to use for making Http Clients to make web requests.</param>
    /// <param name="managerProvider">The <see cref="IManagerProvider{IManagerMetadata}"/> for this manager.</param>
    /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
    /// <returns> BaseProjectManager object </returns>
    public static BaseProvider? CreateProvider(PackageURL purl)
    {
        if (managerProviders.Count == 0)
        {
            managerProviders.AddRange(typeof(BaseProvider).Assembly.GetTypes()
                .Where(type => type.IsSubclassOf(typeof(BaseProvider))));
        }

        // Use reflection to find the correct provider class
        Type? providerClass = managerProviders
            .Where(type => type.Name.Equals($"{purl.Type}Provider",
                StringComparison.InvariantCultureIgnoreCase))
            .FirstOrDefault();

        if (providerClass != null)
        {
            BaseProvider? _provider = Activator.CreateInstance(providerClass) as BaseProvider;

            return _provider;
        }

        return null;
    }

    // do reflection only once
    private static readonly List<Type> managerProviders = new();
}