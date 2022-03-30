// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Helpers;

using Contracts;
using Model.Providers;
using Moq;
using NuGet.Protocol.Core.Types;
using PackageUrl;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

public static class ProviderHelper
{
    /// <summary>
    /// Set up a mock of <see cref="IManagerProviderFactory"/> for this test run.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    /// <param name="managerProvider">The <see cref="IManagerProvider"/> to use when configuring the mocked calls for this manager.</param>
    /// <returns>A Mocked <see cref="IManagerProviderFactory"/>.</returns>
    public static Mock<IManagerProviderFactory> SetupProviderFactory(PackageURL? purl = null, IManagerProvider? managerProvider = null)
    {
        Mock<IManagerProviderFactory> mockProviderFactory = new();
        ManagerProviderFactory realProviderFactory = new();

        if (purl is not null)
        {
            IManagerProvider provider = managerProvider ?? realProviderFactory.CreateProvider(purl);
            mockProviderFactory.Setup(factory => factory.CreateProvider(purl)).Returns(provider);
        }
        else
        {
            mockProviderFactory.Setup(factory => factory.CreateProvider(It.IsAny<PackageURL>())).Returns(
                (PackageURL p) => managerProvider ?? realProviderFactory.CreateProvider(p));
        }
        
        return mockProviderFactory;
    }
    
    /// <summary>
    /// Set up a mock of <see cref="IManagerProvider"/> for this test run.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    /// <param name="metadata">The <see cref="IManagerMetadata"/> to use when returning the call to
    /// <see cref="IManagerProvider.GetMetadataAsync"/>.</param>
    /// <param name="versions">The list of versions to return when mocking the call 
    /// to <see cref="IManagerProvider.GetAllVersionsAsync"/>.</param>
    /// <param name="validSquats">The list of squats to populate the mock to <see cref="IManagerProvider.DoesPackageExistAsync"/>.</param>
    /// <returns>A Mocked <see cref="IManagerProvider"/>.</returns>
    public static IManagerProvider SetupProvider(PackageURL? purl = null,  IManagerMetadata? metadata = null, IEnumerable<string>? versions = null, IEnumerable<string>? validSquats = null)
    {
        Mock<IManagerProvider> mockProvider = new();

        if (purl is not null)
        {
            if (metadata is not null)
            {
                // Mock the metadata call if metadata was provided.
                mockProvider.Setup(provider => provider.GetMetadataAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name) && (purl.Version == null || p.Version.Equals(purl.Version))), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    metadata);
            }

            if (versions is not null)
            {
                // Mock the list of versions if the list was provided.
                IEnumerable<string> versionsArray = versions as string[] ?? versions.ToArray();
                mockProvider.Setup(provider => provider.GetAllVersionsAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    versionsArray);

                // Mock the call to GetLatestVersionAsync to be the last version in the list that was provided.
                mockProvider.Setup(provider => provider.GetLatestVersionAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    versionsArray.Last());
            }
        
            if (validSquats is not null)
            {
                // Mock the other packages that "exist" if a list of "valid" squats was provided.
                foreach (PackageURL squatPurl in validSquats.Select(squatPurlString => new PackageURL(squatPurlString)))
                {
                    mockProvider.Setup(provider => provider.DoesPackageExistAsync(
                        It.Is<PackageURL>(p => p.Name.Equals(squatPurl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                        true);
                }
            }

            // Mock that this package exists.
            mockProvider.Setup(provider => provider.DoesPackageExistAsync(
                It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                true);   
        }

        // Return the mocked provider.
        return mockProvider.Object;
    }
}