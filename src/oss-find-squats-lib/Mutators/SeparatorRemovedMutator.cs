// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    /// <summary>
    /// Generates mutations where all separators are removed.
    /// The default separators are '.', '-', '_'.
    /// </summary>
    public class SeparatorRemovedMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.SeparatorRemoved;

        public HashSet<char> Separators { get; set; } = DefaultSeparators.ToHashSet();

        public static ImmutableHashSet<char> DefaultSeparators { get => ImmutableHashSet.Create(new[] { '.', '-', '_' }); }

        /// <summary>
        /// Initializes a <see cref="SeparatorRemovedMutator"/> instance.
        /// Optionally takes in a additional separators, or a list of overriding separators to
        /// replace the default list with.
        /// </summary>
        /// <param name="additionalSeparators">An optional parameter for extra separators.</param>
        /// <param name="overrideSeparators">An optional parameter for list of separators to replace the
        /// default list with.</param>
        public SeparatorRemovedMutator(char[]? additionalSeparators = null, char[]? overrideSeparators = null)
        {
            if (overrideSeparators != null)
            {
                Separators = new HashSet<char>(overrideSeparators);
            }
            if (additionalSeparators != null)
            {
                Separators = new HashSet<char>(Separators.Concat(additionalSeparators));
            }
        }

        /// <summary>
        /// Generates mutations by removing each separator.
        /// For example: foo-js generates foojs.
        /// </summary>
        /// <param name="target">String to mutate</param>
        /// <returns>The generated mutations.</returns>
        public IEnumerable<Mutation> Generate(string target)
        {
            foreach (char separator in Separators)
            {
                if (target.Contains(separator))
                {
                    yield return new Mutation(
                        mutated: target.Replace(separator.ToString(), string.Empty),
                        original: target,
                        mutator: Kind,
                        reason: $"Separator Removed {separator}");
                }
            }
        }
    }
}