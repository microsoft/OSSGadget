// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Helpers;

using Contracts;
using Moq;
using PackageUrl;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static class PackageActionsHelper<T, TM> where TM : class, IManagerPackageVersionMetadata where T : class, IManagerPackageActions<TM>
{
    /// <summary>
    /// Set up a mock of <see cref="T"/> for this test run.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    /// <param name="metadata">The <see cref="IManagerPackageVersionMetadata"/> to use when returning the call to
    /// <see cref="T"/>.</param>
    /// <param name="versions">The list of versions to return when mocking the call 
    /// to <see cref="T"/>.</param>
    /// <param name="validSquats">The list of squats to populate the mock to <see cref="T"/>.</param>
    /// <returns>A Mocked <see cref="TM"/>.</returns>
    public static T? SetupPackageActions(PackageURL? purl = null, TM? metadata = null, IEnumerable<string>? versions = null, IEnumerable<string>? validSquats = null) 
    {
        Mock<T> mockPackageActions = new();

        if (purl is not null)
        {
            if (metadata is not null)
            {
                // Mock the metadata call if metadata was provided.
                mockPackageActions.Setup(actions => actions.GetMetadataAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name) && (purl.Version == null || p.Version.Equals(purl.Version))), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    metadata);
            }

            if (versions is not null)
            {
                // Mock the list of versions if the list was provided.
                IEnumerable<string> versionsArray = versions as string[] ?? versions.ToArray();
                mockPackageActions.Setup(actions => actions.GetAllVersionsAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    versionsArray);

                // Mock the call to GetLatestVersionAsync to be the last version in the list that was provided.
                mockPackageActions.Setup(actions => actions.GetLatestVersionAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    versionsArray.Last());
            }
        
            if (validSquats is not null)
            {
                // Mock the other packages that "exist" if a list of "valid" squats was provided.
                foreach (PackageURL squatPurl in validSquats.Select(squatPurlString => new PackageURL(squatPurlString)))
                {
                    mockPackageActions.Setup(actions => actions.DoesPackageExistAsync(
                        It.Is<PackageURL>(p => p.Name.Equals(squatPurl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                        true);
                }
            }

            // Mock that this package exists.
            mockPackageActions.Setup(actions => actions.DoesPackageExistAsync(
                It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                true);   
        }

        // Return the mocked package actions object.
        return mockPackageActions.Object as T;
    }
}