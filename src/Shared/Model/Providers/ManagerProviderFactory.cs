// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using Contracts;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Linq;

public class ManagerProviderFactory : IManagerProviderFactory
{

    public ManagerProviderFactory()
    {
    }

    /// <summary>
    /// Get an appropriate implementation of <see cref="IManagerProvider"/> for the project manager in the provided <see cref="PackageURL"/>.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> for the package to create the provider for.</param>
    /// <returns>An implementation of <see cref="IManagerProvider"/> for this project manager.</returns>
    public IManagerProvider CreateProvider(PackageURL purl)
    {
        if (ManagerProviders.Count == 0)
        {
            ManagerProviders.AddRange(typeof(IManagerProvider).Assembly.GetTypes()
                .Where(type => type.IsSubclassOf(typeof(IManagerProvider))));
        }

        // Use reflection to find the correct provider class
        Type? providerClass = ManagerProviders
            .FirstOrDefault(type => type.Name.Equals($"{purl.Type}Provider",
                StringComparison.InvariantCultureIgnoreCase));

        if (providerClass == null)
        {
            throw new NotSupportedException(purl.Type);
        }

        IManagerProvider? _provider = Activator.CreateInstance(providerClass) as IManagerProvider;

        return _provider ?? throw new InvalidOperationException();

    }

    // do reflection only once
    private static readonly List<Type> ManagerProviders = new();
}