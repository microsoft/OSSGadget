// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Helpers;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for if a prefix was added to, or removed from the string.
    /// By default, we check for these prefixes: ".", "x", "-", "X", "_".
    /// </summary>
    public class PrefixMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.Prefix;

        private readonly List<string> _prefixes = new() { ".", "x", "-", "X", "_" };

        /// <summary>
        /// Initializes a <see cref="PrefixMutator"/> instance.
        /// Optionally takes in a additional prefixes, or a list of overriding prefixes to replace the default list with.
        /// </summary>
        /// <param name="additionalPrefixes">An optional parameter for extra prefixes.</param>
        /// <param name="overridePrefixes">An optional parameter for list of prefixes to replace the default list with.</param>
        public PrefixMutator(string[]? additionalPrefixes = null, string[]? overridePrefixes = null, string[]? skipPrefixes = null)
        {
            if (overridePrefixes != null)
            {
                _prefixes = overridePrefixes.ToList();
            }
            if (additionalPrefixes != null)
            {
                _prefixes.AddRange(additionalPrefixes);
            }
            if (skipPrefixes != null)
            {
                _prefixes.RemoveAll(x => skipPrefixes.Contains(x));
            }
        }

        public IEnumerable<Mutation> Generate(string arg)
        {
            // Can't add a character before an @ for a scoped package.
            if (arg.StartsWith('@'))
            {
                var addedPrefixesWithScope = _prefixes.Select(s => new Mutation(
                    mutated: $"@{s}{arg[1..]}",
                    original: arg,
                    mutator: Kind,
                    reason: $"Prefix Added: {s}"));

                var removedPrefixesWithScope = _prefixes.Where(arg[1..].StartsWith).Select(s => new Mutation(
                    mutated: '@' + arg[1..].ReplaceAtStart(s, string.Empty),
                    original: arg,
                    mutator: Kind,
                    reason: $"Prefix Removed: {s}"));

                return addedPrefixesWithScope.Concat(removedPrefixesWithScope);
            }
            
            var addedPrefixes = _prefixes.Select(s => new Mutation(
                mutated: string.Concat(s, arg),
                original: arg,
                mutator: Kind,
                reason: $"Prefix Added: {s}"));
            
            var removedPrefixes = _prefixes.Where(arg.StartsWith).Select(s => new Mutation(
                mutated: arg.ReplaceAtStart(s, string.Empty),
                original: arg,
                mutator: Kind,
                reason: $"Prefix Removed: {s}"));

            return addedPrefixes.Concat(removedPrefixes);
        }
    }
}