// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for adding a character in the string, where the character being added already exists in the string.
    /// </summary>
    /// <example>requests -> reuquests. But won't generate something like requests -> regquests</example>
    public class AddContainingCharacterMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.AddContainingCharacter;

        public IEnumerable<Mutation> Generate(string arg)
        {
            var chars = arg.Distinct().ToArray();
            for (int i = 0; i < arg.Length+1; i++)
            {
                // Can't add a character before an @ for a scoped package.
                if (i == 0 && arg[i] == '@')
                {
                    continue;
                }

                foreach (var c in chars)
                {
                    yield return new Mutation(
                        mutated: $"{arg[..i]}{c}{arg[i..]}",
                        original: arg,
                        mutator: Kind,
                        reason: $"Containing Character Added: {c}");
                }
            }
        }
    }
}