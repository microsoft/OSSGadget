// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.Helpers;

using Contracts;
using Model.Metadata;
using Moq;
using NuGet.Versioning;
using PackageUrl;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static class PackageActionsHelper<T> where T : IManagerPackageVersionMetadata
{
    /// <summary>
    /// Set up a mock of <see cref="IManagerPackageActions{T}"/> for this test run.
    /// </summary>
    /// <param name="purl">The <see cref="PackageURL"/> to use when configuring the mocked calls for this manager.</param>
    /// <param name="metadata">The <see cref="T"/> to use when returning the call to <see cref="IManagerPackageActions{T}.GetMetadataAsync"/>.</param>
    /// <param name="versions">The list of versions (in descending order) to return when mocking the call 
    /// to <see cref="IManagerPackageActions{T}.GetAllVersionsAsync"/>.</param>
    /// <param name="validSquats">The list of squats to populate the mock to <see cref="IManagerPackageActions{T}.DoesPackageExistAsync"/>.</param>
    /// <param name="includePrerelease">If pre-release/beta versions should be included.</param>
    /// <returns>A Mocked <see cref="IManagerPackageActions{T}"/>.</returns>
    public static IManagerPackageActions<T>? SetupPackageActions(
        PackageURL? purl = null,
        T? metadata = default,
        IEnumerable<string>? versions = null,
        IEnumerable<string>? validSquats = null,
        bool includePrerelease = true) 
    {
        Mock<IManagerPackageActions<T>> mockPackageActions = new();

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
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), 
                    includePrerelease, 
                    It.IsAny<bool>(), 
                    It.IsAny<CancellationToken>()).Result)
                    .Returns(() =>
                    {
                        if (typeof(T) != typeof(NuGetPackageVersionMetadata))
                        {
                            return versionsArray;
                        }

                        return versionsArray
                            .Where(v => includePrerelease || !NuGetVersion.Parse(v).IsPrerelease)
                            .Select(v => v.ToString());
                    });

                // Mock the call to GetLatestVersionAsync to be the first version in the list that was provided, as it should be in descending order.
                mockPackageActions.Setup(actions => actions.GetLatestVersionAsync(
                    It.Is<PackageURL>(p => p.Name.Equals(purl.Name)), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()).Result).Returns(
                    versionsArray.First());
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
        return mockPackageActions.Object;
    }
}