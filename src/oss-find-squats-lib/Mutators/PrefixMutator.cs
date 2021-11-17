// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a prefix was added to the string.
    /// We check for these prefixes: ".", "x", "-", "X", "_".
    /// </summary>
    public class PrefixMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.Prefix;


        private static IList<string> _prefixes = new List<string>(){ ".", "x", "-", "X", "_" };

        /// <summary>
        /// Initializes a <see cref="PrefixMutator"/> instance.
        /// Optionally takes in a additional prefixes, or a list of overriding prefixes to replace the default list with.
        /// </summary>
        /// <param name="additionalPrefixes">An optional parameter for extra prefixes.</param>
        /// <param name="overridePrefixes">An optional parameter for list of prefixes to replace the default list with.</param>
        public PrefixMutator(string[]? additionalPrefixes = null, string[]? overridePrefixes = null)
        {
            if (overridePrefixes != null)
            {
                _prefixes = new List<string>(overridePrefixes);
            }
            if (additionalPrefixes != null)
            {
                _prefixes = new List<string>(_prefixes.Concat(additionalPrefixes));
            }
        }

        public IEnumerable<Mutation> Generate(string arg)
        {
            return _prefixes.Select(s => new Mutation()
                {
                    Mutated = string.Concat(s, arg),
                    Original = arg,
                    Mutator = Kind,
                    Reason = "Prefix Added"
                }
            );
        }
    }
}