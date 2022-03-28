// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Providers;

using Contracts;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

public class ProviderFactory : IManagerProviderFactory
{
    public IHttpClientFactory HttpClientFactory { get; }

    public ProviderFactory(IHttpClientFactory? httpClientFactory = null)
    {
        HttpClientFactory = httpClientFactory ?? new DefaultHttpClientFactory();
    }

    /// <summary>
    /// Create a <see cref="BaseProvider"/>.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use in the <see cref="BaseProvider"/>.</param>
    /// <returns>A new <see cref="BaseProvider"/>.</returns>
    public static BaseProvider CreateBaseProvider(IHttpClientFactory httpClientFactory)
    {
        return new BaseProvider(httpClientFactory);
    }

    /// <summary>
    /// Create a <see cref="BaseProvider"/>.
    /// </summary>
    /// <returns>A new <see cref="BaseProvider"/>.</returns>
    public static BaseProvider CreateBaseProvider()
    {
        return new BaseProvider();
    }
    
    /// <summary>
    /// Get an appropriate implementation of <see cref="BaseProvider"/> for the project manager in the provided
    /// <see cref="PackageURL"/>. If no implementation of <see cref="BaseProvider"/> exists for this manager, returns null.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> for the package to create the provider for.</param>
    /// <returns>An implementation of <see cref="BaseProvider"/> for this project manager, or null.</returns>
    public BaseProvider CreateProvider(PackageURL purl)
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
            return new BaseProvider(HttpClientFactory);
        }

        System.Reflection.ConstructorInfo? ctor = providerClass.GetConstructor(new[] { typeof(IHttpClientFactory) });
        if (ctor != null)
        {
            BaseProvider? provider = (BaseProvider)ctor.Invoke(new object?[] { HttpClientFactory });
            return provider;
        }

        return new BaseProvider(HttpClientFactory);
    }

    // do reflection only once
    private static readonly List<Type> ManagerProviders = new();
}