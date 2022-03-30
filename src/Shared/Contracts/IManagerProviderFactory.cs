// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

using PackageUrl;

public interface IManagerProviderFactory
{
    /// <summary>
    /// Creates a new provider that inherits <see cref="IManagerProvider"/>.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to create a provider for.</param>
    /// <returns>A new class that implements <see cref="IManagerProvider"/>.</returns>
    public IManagerProvider CreateProvider(PackageURL purl);
}