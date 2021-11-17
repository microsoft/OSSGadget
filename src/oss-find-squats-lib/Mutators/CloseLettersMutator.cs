// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared.Extensions;
using Microsoft.CST.OpenSource.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a character that was close on the QWERTY Keyboard was used instead.
    /// e.g. r -> f, y -> u, etc.
    /// </summary>
    public class CloseLettersMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.CloseLetters;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (var c in n)
                {
                    yield return new Mutation()
                    {
                        Mutated = arg.ReplaceCharAtPosition(c, i),
                        Original = arg,
                        Mutator = Kind,
                        Reason = "Close Letters on QWERTY Keyboard"
                    };
                }
            }
        }
    }
}