// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;

    /// <summary>
    /// Generates mutations for if a known substitution exists.
    /// e.g. js -> javascript and vice versa.
    /// </summary>
    /// <remarks>
    /// Currently has no substitutions by default, has to be populated in the constructor.
    /// </remarks>
    public class SubstitutionMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.Substitution;

        /// <summary>
        /// A list of the original strings and their equivalent substitutions.
        /// They get replaced in both directions.
        /// e.g. "js" -> "javascript" and "javascript" -> "js".
        /// </summary>
        private readonly IList<(string Original, string Substitution)> _substitutions;

        public SubstitutionMutator(IList<(string Original, string Substitution)>? substitutions = null)
        {
            _substitutions = substitutions ?? new List<(string Original, string Substitution)>();
        }

        public IEnumerable<Mutation> Generate(string arg)
        {
            foreach ((string original, string substitution) in _substitutions)
            {
                if (arg.Contains(original))
                {
                    yield return new Mutation(
                        mutated: arg.Replace(original, substitution),
                        original: arg,
                        mutator: Kind,
                        reason: $"String Substituted: {original} => {substitution}");
                }

                if (arg.Contains(substitution))
                {
                    yield return new Mutation(
                        mutated: arg.Replace(substitution, original),
                        original: arg,
                        mutator: Kind,
                        reason: $"String Substituted: {substitution} => {original}");
                }
            }
        }
    }
}