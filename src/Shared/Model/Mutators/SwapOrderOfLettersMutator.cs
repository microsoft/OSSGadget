// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for if a letter was swapped with an adjacent letter.
    /// </summary>
    public class SwapOrderOfLettersMutator : BaseMutator
    {
        public new string Mutator = "LETTERS_SWAPPED";

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            if (arg.Length < 5)
            {
                // Why?
                yield break;
            }

            for (int i = 1; i < arg.Length - 1; i++)
            {
                yield return (string.Concat(arg.Substring(0, i), arg[i + 1], arg[i], arg.Substring(i + 2, arg.Length - (i + 2))), Mutator);
            }
        }
    }
}