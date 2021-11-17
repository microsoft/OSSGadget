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
        public MutatorType Kind { get; } = MutatorType.SwapOrderOfLetters;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 1; i < arg.Length - 1; i++)
            {
                yield return new Mutation()
                {
                    Mutated = string.Concat(arg[..i], arg[i + 1], arg[i], arg.Substring(i + 2, arg.Length - (i + 2))),
                    Original = arg,
                    Mutator = Kind,
                    Reason = "Letters Swapped"
                };
            }
        }
    }
}