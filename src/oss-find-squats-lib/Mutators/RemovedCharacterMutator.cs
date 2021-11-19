// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;

    /// <summary>
    /// Generates mutations for removing a character in the string.
    /// </summary>
    public class RemovedCharacterMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.RemovedCharacter;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 1; i < arg.Length; i++)
            {
                yield return new Mutation(
                    mutated: $"{arg[..i]}{arg[(i + 1)..]}",
                    original: arg,
                    mutator: Kind,
                    reason: $"Character Removed: {i} ('{arg[i]}')");
            }
        }
    }
}