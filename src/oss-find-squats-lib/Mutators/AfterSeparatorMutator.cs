// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a character was changed after a separator was used.
    /// </summary>
    public class AfterSeparatorMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.AfterSeparator;

        public HashSet<char> Separators { get; set; } = SeparatorMutator.DefaultSeparators.ToHashSet();

        public AfterSeparatorMutator(char[]? additionalSeparators = null, char[]? overrideSeparators = null, char[]? skipSeparators = null)
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

        public IEnumerable<Mutation> Generate(string arg)
        {
            foreach (char s in Separators)
            {
                string[] splits = arg.Split(s, StringSplitOptions.RemoveEmptyEntries);

                if (splits.Length >= 2)
                {
                    for (int i = 0; i < splits.Length - 1; i++)
                    {
                        for (char c = 'a'; c <= 'z'; c++)
                        {
                            yield return new Mutation(
                                mutated: splits[i] + s + c + string.Join('s', splits[(i + 1)..]),
                                original: arg,
                                mutator: Kind,
                                reason: $"After Separator: {s}");
                        }
                    }
                }
            }
        }
    }
}