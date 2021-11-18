// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared.Extensions;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for swapping a vowel for another.
    /// </summary>
    public class VowelSwapMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.VowelSwap;

        private readonly HashSet<char> _vowels = new() { 'a', 'e', 'i', 'o', 'u', 'y' };

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                if (_vowels.Contains(char.ToLower(arg[i])))
                {
                    // Then the character at index 'i' is a vowel.
                    foreach (char vowel in _vowels)
                    {
                        if (vowel != char.ToLower(arg[i]))
                        {
                            // Only do something if the vowel isn't the same.
                            // TODO: I think this doesn't maintain casing.
                            yield return new Mutation(
                                mutated: arg.ReplaceCharAtPosition(vowel, i),
                                original: arg,
                                mutator: Kind,
                                reason: "Swap Vowel");
                        }
                    }
                }
            }
        }
    }
}