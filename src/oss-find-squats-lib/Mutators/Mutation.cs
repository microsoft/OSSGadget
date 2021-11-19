// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// A record of the mutation genrated by an IMutator.
    /// </summary>
    public record Mutation
    {
        /// <summary>
        /// Construct a Mutation.
        /// </summary>
        /// <param name="mutated">The mutated string.</param>
        /// <param name="original">The original string.</param>
        /// <param name="reason">A human readable explanation of the change made to the original string.</param>
        /// <param name="mutator">The MutatorType of the mutator which generated this.</param>
        public Mutation(string mutated, string original, string reason, MutatorType mutator)
        {
            Mutated = mutated;
            Original = original;
            Reason = reason;
            Mutator = mutator;
        }

        /// <summary>
        /// The mutated string.
        /// </summary>
        public string Mutated { get; }
        /// <summary>
        /// The original string.
        /// </summary>
        public string Original { get; }
        /// <summary>
        /// A human readable explanation of the change made to the original string.
        /// </summary>
        public string Reason { get; }
        /// <summary>
        /// The MutatorType of the mutator which generated this.
        /// </summary>
        public MutatorType Mutator { get; }
    }
}