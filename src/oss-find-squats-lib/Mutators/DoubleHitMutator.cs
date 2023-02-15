// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for if a nearby character was double pressed.
    /// </summary>
    public class DoubleHitMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.DoubleHit;

        private readonly List<char> _excludedCharacters = new() { ',', '[', ']', '=', ';', '\'' };

        /// <summary>
        /// Initializes a <see cref="DoubleHitMutator"/> instance.
        /// Optionally takes in a additional characters to be excluded, or a list of overriding characters to be excluded to replace the default list with.
        /// </summary>
        /// <param name="additionalExcludedChars">An optional parameter for extra characters to be excluded.</param>
        /// <param name="overrideExcludedChars">An optional parameter for list of characters to be excluded to replace the default list with.</param>
        public DoubleHitMutator(char[]? additionalExcludedChars = null, char[]? overrideExcludedChars = null, char[]? skipExcludedChars = null)
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
                List<char> n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (char c in n)
                {
                    // We don't want mutations like pkg:npm//lodash, that is the same as pkg:npm/lodash
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
                        mutated: string.Concat(arg[..i], c, arg[i..]),
                        original: arg,
                        mutator: Kind,
                        reason: $"Double hit character: {c}");
                }
            }
        }
    }
}