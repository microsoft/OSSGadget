// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for if a character was added after a separator was used.
    /// </summary>
    public class RemoveSeparatedSectionMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.RemoveSeparatedSection;

        public HashSet<char> Separators { get; set; } = SeparatorMutator.DefaultSeparators.ToHashSet();

        public RemoveSeparatedSectionMutator(char[]? additionalSeparators = null, char[]? overrideSeparators = null, char[]? skipSeparators = null)
        {
            if (overrideSeparators != null)
            {
                Separators = new HashSet<char>(overrideSeparators);
            }
            if (additionalSeparators != null)
            {
                Separators.UnionWith(additionalSeparators);
            }
            if (skipSeparators != null)
            {
                Separators.ExceptWith(skipSeparators);
            }
        }

        /// <summary>
        /// Generates mutations by adding a character after each separator.
        /// For example: foo-js generates foo-ajs, foo-bjs, etc. through z.
        /// </summary>
        /// <param name="target">String to mutate</param>
        /// <returns>The generated mutations.</returns>
        public IEnumerable<Mutation> Generate(string arg)
        {
            foreach (char separator in Separators)
            {
                string[] splits = arg.Split(separator, StringSplitOptions.RemoveEmptyEntries);

                if (splits.Length >= 2)
                {
                    for (int i = 0; i < splits.Length; i++)
                    {
                        yield return new Mutation(
                            mutated: $"{string.Join(separator, splits[..i])}{string.Join(separator, splits[(i + 1)..])}",
                            original: arg,
                            mutator: Kind,
                            reason: $"Separated Section Removed: {i} ('{arg[i]}')");
                    }
                }
            }
        }
    }
}