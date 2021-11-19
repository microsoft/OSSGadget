// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a suffix was added to the string.
    /// By default, we check for these prefixes: "s", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "ng", "-ng", ".", "x", "-", "_".
    /// </summary>
    public class SuffixMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.Suffix;

        private readonly List<string> _suffixes = new() { "s", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "ng", "-ng", ".", "x", "-", "_" };

        /// <summary>
        /// Initializes a <see cref="SuffixMutator"/> instance.
        /// Optionally takes in a additional suffixes, or a list of overriding suffixes to replace the default list with.
        /// </summary>
        /// <param name="additionalSuffixes">An optional parameter for extra suffixes.</param>
        /// <param name="overrideSuffixes">An optional parameter for list of suffixes to replace the default list with.</param>
        public SuffixMutator(string[]? additionalSuffixes = null, string[]? overrideSuffixes = null, string[]? skipSuffixes = null)
        {
            if (overrideSuffixes != null)
            {
                _suffixes = overrideSuffixes.ToList();
            }
            if (additionalSuffixes != null)
            {
                _suffixes.AddRange(additionalSuffixes);
            }
            if (skipSuffixes != null)
            {
                _suffixes.RemoveAll(x => skipSuffixes.Contains(x));
            }
        }
        public IEnumerable<Mutation> Generate(string arg)
        {
            return _suffixes.Select(s => new Mutation(
                    mutated: string.Concat(arg, s),
                    original: arg,
                    mutator: Kind,
                    reason: $"Suffix Added: {s}"));
        }
    }
}