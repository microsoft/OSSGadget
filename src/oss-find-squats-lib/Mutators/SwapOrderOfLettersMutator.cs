// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a letter was swapped with an adjacent letter.
    /// </summary>
    public class SwapOrderOfLettersMutator : Mutator
    {
        public string Name { get; } = "LETTERS_SWAPPED";

        public IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            if (arg.Length < 5)
            {
                // TODO: Figure out why we only check for less than 5 char strings.
                yield break;
            }

            for (int i = 1; i < arg.Length - 1; i++)
            {
                yield return (string.Concat(arg[..i], arg[i + 1], arg[i], arg.Substring(i + 2, arg.Length - (i + 2))), Name);
            }
        }
    }
}