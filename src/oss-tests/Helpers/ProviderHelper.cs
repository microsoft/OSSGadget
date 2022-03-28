// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Helpers;

using Contracts;
using Model.Providers;
using Moq;
using PackageUrl;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

public static class ProviderHelper
{
    /// <summary>
    /// Set up a mock of <see cref="IManagerProvider{IManagerMetadata}"/> for this test run.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use with the provider.</param>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    /// <param name="managerProvider">The <see cref="IManagerProvider{IManagerMetadata}"/> to use when configuring the mocked calls for this manager.</param>
    /// <returns>A Mocked <see cref="IManagerProviderFactory"/>.</returns>
    public static Mock<IManagerProviderFactory> SetupProviderFactory(IHttpClientFactory httpClientFactory, PackageURL? purl = null, BaseProvider? managerProvider = null)
    {
        Mock<IManagerProviderFactory> mockProviderFactory = new();
        ProviderFactory realProviderFactory = new(httpClientFactory);

        if (purl is not null)
        {
            BaseProvider provider = managerProvider ?? realProviderFactory.CreateProvider(purl);
            mockProviderFactory.Setup(factory => factory.CreateProvider(purl)).Returns(provider);
        }
        else
        {
            mockProviderFactory.Setup(factory => factory.CreateProvider(It.IsAny<PackageURL>())).Returns(
                (PackageURL p) => managerProvider ?? realProviderFactory.CreateProvider(p));
        }

        mockProviderFactory.Setup(factory => factory.HttpClientFactory).Returns(httpClientFactory);

        return mockProviderFactory;
    }
    
    /// <summary>
    /// Set up a mock of <see cref="IManagerProvider{IManagerMetadata}"/> for this test run.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use with the provider.</param>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    /// <param name="metadata">The <see cref="IManagerMetadata"/> to use when returning the call to
    /// <see cref="IManagerProvider{IManagerMetadata}.GetMetadataAsync"/>.</param>
    /// <param name="versions">The list of versions to return when mocking the call 
    /// to <see cref="IManagerProvider{IManagerMetadata}.GetAllVersionsAsync"/>.</param>
    /// <param name="validSquats">The list of squats to populate the mock to <see cref="IManagerProvider{IManagerMetadata}.DoesPackageExistAsync"/>.</param>
    /// <returns>A Mocked <see cref="IManagerProvider{IManagerMetadata}"/>.</returns>
    public static BaseProvider SetupProvider(IHttpClientFactory httpClientFactory, PackageURL? purl = null,  IManagerMetadata? metadata = null, IEnumerable<string>? versions = null, IEnumerable<string>? validSquats = null)
    {
        Mock<BaseProvider> mockProvider = new();

        if (purl is not null)
        {
            if (metadata is not null)
            {
                mockProvider.Setup(provider => provider.GetMetadataAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name) && (purl.Version == null || p.Version.Equals(purl.Version))), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    metadata);
            }

            if (versions is not null)
            {
                IEnumerable<string> versionsArray = versions as string[] ?? versions.ToArray();
                mockProvider.Setup(provider => provider.GetAllVersionsAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    versionsArray);

                mockProvider.Setup(provider => provider.GetLatestVersionAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    versionsArray.Last());
            }
        
            if (validSquats is not null)
            {
                foreach (PackageURL squatPurl in validSquats.Select((squatPurlString) => new PackageURL(squatPurlString)))
                {
                    mockProvider.Setup(provider => provider.DoesPackageExistAsync(
                        It.Is<PackageURL>(p => p.Name.Equals(squatPurl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                        true);
                }
            }

            mockProvider.Setup(provider => provider.DoesPackageExistAsync(
                It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                true);   
        }

        // mockProvider.Setup(provider => provider.HttpClientFactory).Returns(httpClientFactory);
        mockProvider.Setup(provider => provider.CreateHttpClient()).Returns(httpClientFactory.CreateClient());

        return mockProvider.Object;
    }
}