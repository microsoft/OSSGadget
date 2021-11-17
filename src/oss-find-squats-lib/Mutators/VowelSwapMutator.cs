// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for swapping a vowel for another.
    /// </summary>
    public class VowelSwapMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.VowelSwap;

        private HashSet<char> _vowels = new() {'a', 'e', 'i', 'o', 'u', 'y'};

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (var i = 0; i < arg.Length; i++)
            {
                if (_vowels.Contains(char.ToLower(arg[i])))
                {
                    // Then the character at index 'i' is a vowel.
                    foreach (var vowel in _vowels)
                    {
                        if (vowel != char.ToLower(arg[i]))
                        {
                            // Only do something if the vowel isn't the same.
                            // TODO: I think this doesn't maintain casing.
                            yield return new Mutation()
                            {
                                Mutated = arg.ReplaceCharAtPosition(vowel, i),
                                Original = arg,
                                Mutator = Kind,
                                Reason = "Swap Vowel"
                            };
                        }
                    }
                }
            }
        }
    }
}