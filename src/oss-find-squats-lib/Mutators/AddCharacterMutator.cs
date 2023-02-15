// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for adding a character in the string.
    /// </summary>
    public class AddCharacterMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.AddCharacter;

        private readonly List<char> _characters = new()
        {
            '1','2','3','4','5','6','7','8','9','0','-','q','w','e','r','t','y','u','i','o','p','a','s','d','f','g','h','j','k','l','z','x','c','v','b','n','m','.',
        };

        /// <summary>
        /// Initializes a <see cref="AddCharacterMutator"/> instance.
        /// Optionally takes in a additional characters to be added, or a list of overriding characters to be added to replace the default list with.
        /// </summary>
        /// <param name="additionalChars">An optional parameter for extra characters to be added.</param>
        /// <param name="overrideChars">An optional parameter for list of characters to be added to replace the default list with.</param>
        public AddCharacterMutator(char[]? additionalChars = null, char[]? overrideChars = null, char[]? skipChars = null)
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
            for (int i = 0; i < arg.Length+1; i++)
            {
                // Can't add a character before an @ for a scoped package.
                if (i == 0 && arg[i] == '@')
                {
                    continue;
                }

                foreach (var c in _characters)
                {
                    yield return new Mutation(
                        mutated: $"{arg[..i]}{c}{arg[(i)..]}",
                        original: arg,
                        mutator: Kind,
                        reason: $"Character Added: {c}");
                }
            }
        }
    }
}