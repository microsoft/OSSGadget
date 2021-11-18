// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// The mutator interface
    /// </summary>
    public interface IMutator
    {
        /// <summary>
        /// An enum specifying which kind of Mutator this is.
        /// </summary>
        public MutatorType Kind { get; }

        /// <summary>
        /// Generates the typo squat mutations for a string.
        /// </summary>
        /// <param name="arg">The string to generate mutations for.</param>
        /// <returns>A list of mutations with the name and reason.</returns>
        public IEnumerable<Mutation> Generate(string arg);
    }
}