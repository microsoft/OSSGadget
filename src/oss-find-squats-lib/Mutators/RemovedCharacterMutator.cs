// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for removing a character in the string.
    /// </summary>
    public class RemovedCharacterMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.RemovedCharacter;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (var i = 1; i < arg.Length; i++)
            {
                yield return new Mutation(
                    mutated: $"{arg[..i]}{arg[(i + 1)..]}",
                    original: arg,
                    mutator: Kind,
                    reason: "Character Removed");
            }
        }
    }
}