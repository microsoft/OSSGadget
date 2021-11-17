// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    public record Mutation
    {
        public Mutation(string mutated, string original, string reason, MutatorType mutator)
        {
            Mutated = mutated;
            Original = original;
            Reason = reason;
            Mutator = mutator;
        }
        public string Mutated { get; }
        public string Original { get; }
        public string Reason { get; }
        public MutatorType Mutator { get; }
    }
}