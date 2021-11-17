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
        public string Name { get; } = "CLOSE_LETTERS_ON_KEYBOARD";

        public IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (var c in n)
                {
                    yield return (arg.ReplaceCharAtPosition(c, i), Name);
                }
            }
        }
    }
}