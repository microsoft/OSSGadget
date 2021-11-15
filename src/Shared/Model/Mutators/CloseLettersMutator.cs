// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for if a character that was close on the QWERTY Keyboard was used instead.
    /// e.g. r -> f, y -> u, etc.
    /// </summary>
    public class CloseLettersMutator : BaseMutator
    {
        public new string Mutator = "CLOSE_LETTERS_ON_KEYBOARD";

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (var c in n)
                {
                    yield return (arg.ReplaceCharAtPosition(c, i), Mutator);
                }
            }
        }
    }
}