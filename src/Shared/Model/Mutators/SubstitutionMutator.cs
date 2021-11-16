// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for if a known substitution exists.
    /// e.g. js -> javascript and vice versa.
    /// </summary>
    /// <remarks>
    /// Currently has no substitutions by default, has to be populated in the constructor.
    /// </remarks>
    public class SubstitutionMutator : BaseMutator
    {
        public new string Mutator = "SWAPPED_WITH_SUBSTITUTE";

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

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            foreach (var (original, substitution) in _substitutions)
            {
                if (arg.Contains(original))
                {
                    yield return (arg.Replace(original, substitution), Mutator);
                }

                if (arg.Contains(substitution))
                {
                    yield return (arg.Replace(substitution, original), Mutator);
                }
            }
        }
    }
}