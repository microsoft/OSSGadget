// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Helpers;

using Contracts;
using Moq;
using PackageUrl;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static class ProviderHelper
{
    /// <summary>
    /// Set up the <see cref="Mock{IManagerProvider{IManagerMetadata}}"/> for this test run.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    public static Mock<IManagerProvider<IManagerMetadata>>? SetupProvider(PackageURL purl, IManagerMetadata? metadata = null, IEnumerable<string>? versions = null, IEnumerable<string>? validSquats = null)
    {
        Mock<IManagerProvider<IManagerMetadata>> mockProvider = new();

        if (metadata is not null)
        {
            mockProvider.Setup(provider => provider.GetMetadataAsync(
                It.Is<PackageURL>(p => p.Name.Equals(purl.Name) && (purl.Version == null || p.Version.Equals(purl.Version))), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                metadata);
        }

        if (versions is not null)
        {
            mockProvider.Setup(provider => provider.GetAllVersionsAsync(
                It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                versions);
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

        return mockProvider;
    }
}