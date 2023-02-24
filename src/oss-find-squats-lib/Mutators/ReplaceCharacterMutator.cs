// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Helpers;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for replacing a character in the string.
    /// </summary>
    public class ReplaceCharacterMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.ReplaceCharacter;

        private readonly List<char> _characters = new()
        {
            '1','2','3','4','5','6','7','8','9','0','-','q','w','e','r','t','y','u','i','o','p','a','s','d','f','g','h','j','k','l','z','x','c','v','b','n','m','.',
        };

        /// <summary>
        /// Initializes a <see cref="ReplaceCharacterMutator"/> instance.
        /// Optionally takes in a additional characters to be used when replacing, or a list of overriding characters to be used when replacing to replace the default list with.
        /// </summary>
        /// <param name="additionalChars">An optional parameter for extra characters to be used when replacing.</param>
        /// <param name="overrideChars">An optional parameter for list of characters to be used when replacing to replace the default list with.</param>
        public ReplaceCharacterMutator(char[]? additionalChars = null, char[]? overrideChars = null, char[]? skipChars = null)
        {
            if (overrideChars != null)
            {
                _characters = overrideChars.ToList();
            }
            if (additionalChars != null)
            {
                _characters.AddRange(additionalChars);
            }
            if (skipChars != null)
            {
                _characters.RemoveAll(skipChars.Contains);
            }
        }

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                // If the first character is an '@' we don't want to replace it, as that would break it being a scoped package.
                if (i == 0 && arg[i] == '@')
                {
                    continue;
                }

                foreach (var c in _characters)
                {
                    // Continue if the character would be replaced with itself.
                    if (char.ToLower(arg[i]) == c)
                    {
                        continue;
                    }

                    yield return new Mutation(
                        mutated: arg.ReplaceCharAtPosition(c, i),
                        original: arg,
                        mutator: Kind,
                        reason: $"Character Replaced: {c}");
                }
            }
        }
    }
}