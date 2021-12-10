// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Lib.Helpers;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for if a character that was close on the QWERTY Keyboard was used instead.
    /// e.g. r -> f, y -> u, etc.
    /// </summary>
    public class CloseLettersMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.CloseLetters;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                List<char>? n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (char c in n)
                {
                    yield return new Mutation(
                        mutated: arg.ReplaceCharAtPosition(c, i),
                        original: arg,
                        mutator: Kind,
                        reason: $"Close Letters on QWERTY Keyboard: {arg[i]} => {c}");
                }
            }
        }
    }
}