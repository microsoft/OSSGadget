// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a character was duplicated in the string. Or duplicated and replaced.
    /// </summary>
    public class DuplicatorMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.Duplicator;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                yield return new Mutation(
                    mutated: string.Concat(arg[..i], arg[i], arg[i..]),
                    original: arg,
                    mutator: Kind,
                    reason: $"Letter Duplicated: {arg[i]}");
            }

            for (int i = 0; i < arg.Length - 2; i++)
            {
                yield return new Mutation(
                    mutated: string.Concat(arg[..(i + 1)], arg[i], arg[(i + 2)..]),
                    original: arg,
                    mutator: Kind,
                    reason: "Letter Duplicated and Replaced");
            }

            yield return new Mutation(
                mutated: string.Concat(arg[..arg.Length], arg[^1]),
                original: arg,
                mutator: Kind,
                reason: "Letter Duplicated and Replaced");
        }
    }
}