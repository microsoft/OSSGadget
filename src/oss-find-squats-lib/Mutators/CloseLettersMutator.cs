// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Microsoft.CST.OpenSource.Helpers;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for if a character that was close on the QWERTY Keyboard was used instead.
    /// e.g. r -> f, y -> u, etc.
    /// </summary>
    public class CloseLettersMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.CloseLetters;
        
        private readonly List<char> _excludedCharacters = new() { ',', '[', ']', '=', ';', '\'' };

        /// <summary>
        /// Initializes a <see cref="CloseLettersMutator"/> instance.
        /// Optionally takes in a additional characters to be excluded, or a list of overriding characters to be excluded to replace the default list with.
        /// </summary>
        /// <param name="additionalExcludedChars">An optional parameter for extra characters to be excluded.</param>
        /// <param name="overrideExcludedChars">An optional parameter for list of characters to be excluded to replace the default list with.</param>
        public CloseLettersMutator(char[]? additionalExcludedChars = null, char[]? overrideExcludedChars = null, char[]? skipExcludedChars = null)
        {
            if (overrideExcludedChars != null)
            {
                _excludedCharacters = overrideExcludedChars.ToList();
            }
            if (additionalExcludedChars != null)
            {
                _excludedCharacters.AddRange(additionalExcludedChars);
            }
            if (skipExcludedChars != null)
            {
                _excludedCharacters.RemoveAll(skipExcludedChars.Contains);
            }
        }

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                List<char>? n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (char c in n)
                {
                    // We don't want mutations like pkg:npm//odash mutated from pkg:npm/lodash that is the same as pkg:npm/odash
                    if (i == 0 && c == '/')
                    {
                        continue;
                    }

                    // We don't want mutations that include any of the characters we want to exclude.
                    if (_excludedCharacters.Contains(c))
                    {
                        continue;
                    }

                    yield return new Mutation(
                        mutated: arg.ReplaceCharAtPosition(c, i),
                        original: arg,
                        mutator: Kind,
                        reason: $"Close Letters on QWERTY Keyboard: {arg[i]} => {c}");
                }
            }
            
            // Also try adding an additional character at the end of neighbouring characters.
            List<char>? n2 = QwertyKeyboardHelper.GetNeighboringCharacters(arg.Last()).ToList();

            foreach (char c in n2)
            {
                // We don't want mutations that include any of the characters we want to exclude.
                if (_excludedCharacters.Contains(c))
                {
                    continue;
                }

                yield return new Mutation(
                    mutated: string.Concat(arg, c),
                    original: arg,
                    mutator: Kind,
                    reason: $"Close Letters on QWERTY Keyboard, added to end: {arg} => {c}");
            }
        }
    }
}