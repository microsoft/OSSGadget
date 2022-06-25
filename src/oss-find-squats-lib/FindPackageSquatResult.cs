// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

#pragma warning disable CS8618
namespace Microsoft.CST.OpenSource.FindSquats
{
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
        
        [JsonConstructor]
        public FindPackageSquatResult(string mutatedPackageName, PackageURL mutatedPackageUrl, PackageURL originalPackageUrl, IEnumerable<Mutation> mutations)
        {
            MutatedPackageName = mutatedPackageName;
            MutatedPackageUrl = mutatedPackageUrl;
            OriginalPackageUrl = originalPackageUrl;
            Mutations = mutations;
        }

        /// <summary>
        /// The squatting package name.
        /// </summary>
        [JsonInclude]
        public string MutatedPackageName { get; }
        /// <summary>
        /// The <see cref="PackageURL"/> with that is squatting on the OriginalPackageUrl.
        /// </summary>
        [JsonInclude]
        public PackageURL MutatedPackageUrl { get; }
        /// <summary>
        /// The <see cref="PackageURL"/> with uniquely identifying information for the original package that the squat was found on.
        /// </summary>
        [JsonInclude]
        public PackageURL OriginalPackageUrl { get; }
        /// <summary>
        /// The reasons this detection was made
        /// </summary>
        [JsonIgnore]
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
            return JsonSerializer.Serialize(this, OssGadgetJsonSerializer.Options);
        }

        /// <summary>
        /// Converts a json string into a <see cref="FindPackageSquatResult"/>.
        /// </summary>
        /// <param name="json">The json string representing the <see cref="FindPackageSquatResult"/>.</param>
        /// <returns>A new <see cref="FindPackageSquatResult"/> constructed from the json string.</returns>
        /// <exception cref="InvalidCastException">If the json string cannot be deserialized into a <see cref="FindPackageSquatResult"/>.</exception>
        public static FindPackageSquatResult FromJsonString(string json)
        {
            return JsonSerializer.Deserialize<FindPackageSquatResult>(json, OssGadgetJsonSerializer.Options) ?? throw new InvalidCastException();
        }
    }
}
