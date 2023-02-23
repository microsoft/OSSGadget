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
            for (int i = 0; i < arg.Length; i++)
            {
                // Don't want to remove an @ for scoped npm package as part of mutations. It's ultimately pointless to do so.
                if (i == 0 && arg[i] == '@')
                {
                    continue;
                }
                yield return new Mutation(
                    mutated: $"{arg[..i]}{arg[(i + 1)..]}",
                    original: arg,
                    mutator: Kind,
                    reason: $"Character Removed: {i} ('{arg[i]}')");
            }
        }
    }
}