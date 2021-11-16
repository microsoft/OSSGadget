// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for swapping a vowel for another.
    /// </summary>
    public class VowelSwapMutator : BaseMutator
    {
        public new string Mutator = "VOWEL_SWAPPED";

        private HashSet<char> _vowels = new() {'a', 'e', 'i', 'o', 'u', 'y'};

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for(var i = 0; i < arg.Length; i++)
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
                            yield return (arg.ReplaceCharAtPosition(vowel, i), Mutator);
                        }
                    }
                }
            }
        }
    }
}