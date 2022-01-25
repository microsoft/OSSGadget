// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats
{
    using Microsoft.CST.OpenSource.FindSquats.Mutators;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// Represents a potential squatted package.
    /// </summary>
    public class FindPackageSquatResult
    {
        public FindPackageSquatResult(string packageName, PackageURL packageUrl, PackageURL squattedPackage, IEnumerable<Mutation> mutations)
        {
            PackageName = packageName;
            PackageUrl = packageUrl;
            SquattedPackage = squattedPackage;
            Mutations = mutations;
        }
        /// <summary>
        /// The name of the package
        /// </summary>
        public string PackageName { get; }
        /// <summary>
        /// The <see cref="PackageURL"/> with uniquely identifying information for the package in its repository.
        /// </summary>
        public PackageURL PackageUrl { get; }
        /// <summary>
        /// The <see cref="PackageURL"/> with uniquely identifying information for the package that appears to be squatted.
        /// </summary>
        public PackageURL SquattedPackage { get; }
        /// <summary>
        /// The reasons this detection was made
        /// </summary>
        public IEnumerable<string> Rules => Mutations.Select(x => x.Reason);
        /// <summary>
        /// The <see cref="Mutation"/>s that generated this PackageName
        /// </summary>
        public IEnumerable<Mutation> Mutations { get; }

        /// <summary>
        /// Converts this <see cref="FindPackageSquatResult"/> to a json string.
        /// </summary>
        /// <returns>A json string representing this instance of <see cref="FindPackageSquatResult"/>.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
