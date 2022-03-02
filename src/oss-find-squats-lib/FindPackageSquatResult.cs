// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

#pragma warning disable CS8618
namespace Microsoft.CST.OpenSource.FindSquats
{
    using Microsoft.CST.OpenSource.Helpers;
    using Microsoft.CST.OpenSource.FindSquats.Mutators;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a potential squatted package.
    /// </summary>
    public class FindPackageSquatResult
    {
        // Custom json serialization converter for PackageURLs
        private static JsonSerializerOptions _serializeOptions = new()
        {
            Converters =
            {
                new PackageUrlJsonConverter()
            }
        };

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
        [JsonInclude]
        public string PackageName { get; }
        /// <summary>
        /// The <see cref="PackageURL"/> with uniquely identifying information for the package in its repository.
        /// </summary>
        [JsonInclude]
        public PackageURL PackageUrl { get; }
        /// <summary>
        /// The <see cref="PackageURL"/> with uniquely identifying information for the package that appears to be squatted.
        /// </summary>
        [JsonInclude]
        public PackageURL SquattedPackage { get; }
        /// <summary>
        /// The reasons this detection was made
        /// </summary>
        public IEnumerable<string> Rules => Mutations.Select(x => x.Reason);
        /// <summary>
        /// The <see cref="Mutation"/>s that generated this PackageName
        /// </summary>
        [JsonInclude]
        public IEnumerable<Mutation> Mutations { get; }

        /// <summary>
        /// Converts this <see cref="FindPackageSquatResult"/> to a json string.
        /// </summary>
        /// <returns>A json string representing this instance of <see cref="FindPackageSquatResult"/>.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, _serializeOptions);
        }

        /// <summary>
        /// Converts a json string into a <see cref="FindPackageSquatResult"/>.
        /// </summary>
        /// <param name="json">The json string representing the <see cref="FindPackageSquatResult"/>.</param>
        /// <returns>A new <see cref="FindPackageSquatResult"/> constructed from the json string.</returns>
        /// <exception cref="InvalidCastException">If the json string cannot be deserialized into a <see cref="FindPackageSquatResult"/>.</exception>
        public static FindPackageSquatResult FromJsonString(string json)
        {
            return JsonSerializer.Deserialize<FindPackageSquatResult>(json, _serializeOptions) ?? throw new InvalidCastException();
        }
    }
}
