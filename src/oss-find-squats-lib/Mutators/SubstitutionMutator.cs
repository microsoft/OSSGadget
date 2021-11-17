// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a known substitution exists.
    /// e.g. js -> javascript and vice versa.
    /// </summary>
    /// <remarks>
    /// Currently has no substitutions by default, has to be populated in the constructor.
    /// </remarks>
    public class SubstitutionMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.Substitution;


        /// <summary>
        /// A list of the original strings and their equivalent substitutions.
        /// They get replaced in both directions.
        /// e.g. "js" -> "javascript" and "javascript" -> "js".
        /// </summary>
        private static IList<(string Original, string Substitution)> _substitutions = new List<(string Original, string Substitution)>();

        public SubstitutionMutator(IList<(string Original, string Substitution)>? substitutions = null)
        {
            if (substitutions != null)
            {
                _substitutions = substitutions;
            }
        }

        public IEnumerable<Mutation> Generate(string arg)
        {
            foreach (var (original, substitution) in _substitutions)
            {
                if (arg.Contains(original))
                {
                    yield return new Mutation()
                    {
                        Mutated = arg.Replace(original, substitution),
                        Original = arg,
                        Mutator = Kind,
                        Reason = "Character Substituted"
                    };
                }

                if (arg.Contains(substitution))
                {
                    yield return new Mutation()
                    {
                        Mutated = arg.Replace(substitution, original),
                        Original = arg,
                        Mutator = Kind,
                        Reason = "Character Substituted"
                    };
                }
            }
        }
    }
}