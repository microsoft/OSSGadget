// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// The base mutator to be implemented by other mutators.
    /// </summary>
    public interface Mutator
    {
        public MutatorType Kind { get; }

        /// <summary>
        /// Generates the typo squat mutations for a string.
        /// </summary>
        /// <param name="arg">The string to generate mutations for.</param>
        /// <returns>A list of mutations with the name and reason.</returns>
        public abstract IEnumerable<Mutation> Generate(string arg);
    }
}