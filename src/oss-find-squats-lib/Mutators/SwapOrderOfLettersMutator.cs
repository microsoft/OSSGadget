// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;

    /// <summary>
    /// Generates mutations for if a letter was swapped with an adjacent letter.
    /// </summary>
    public class SwapOrderOfLettersMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.SwapOrderOfLetters;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length - 1; i++)
            {
                // Don't want to swap the same character for the same character, foo -> foo.
                if (arg[i + 1] == arg[i])
                {
                    continue;
                }

                yield return new Mutation(
                    mutated: string.Concat(arg[..i], arg[i + 1], arg[i], arg.Substring(i + 2, arg.Length - (i + 2))),
                    original: arg,
                    mutator: Kind,
                    reason: "Letters Swapped");
            }
        }
    }
}