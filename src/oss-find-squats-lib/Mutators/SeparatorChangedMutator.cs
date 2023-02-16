// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    /// <summary>
    /// Generates mutations where each separator is swapped for another.
    /// The default separators are '.', '-', '_'.
    /// </summary>
    public class SeparatorChangedMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.SeparatorChanged;

        public HashSet<char> Separators { get; set; } = SeparatorRemovedMutator.DefaultSeparators.ToHashSet();

        /// <summary>
        /// Initializes a <see cref="SeparatorChangedMutator"/> instance.
        /// Optionally takes in a additional separators, or a list of overriding separators to
        /// replace the default list with.
        /// </summary>
        /// <param name="additionalSeparators">An optional parameter for extra separators.</param>
        /// <param name="overrideSeparators">An optional parameter for list of separators to replace the
        /// default list with.</param>
        public SeparatorChangedMutator(char[]? additionalSeparators = null, char[]? overrideSeparators = null)
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
        /// Generates mutations by replacing each candidate <see cref="Separators"/> with each other <see cref="Separators"/> and with <see cref="string.Empty"/>
        /// For example: foo-js generates foo.js and foo_js
        /// </summary>
        /// <param name="target">String to mutate</param>
        /// <returns>The generated mutations.</returns>
        public IEnumerable<Mutation> Generate(string target)
        {
            foreach (char separator in Separators)
            {
                if (target.Contains(separator))
                {
                    IEnumerable<char>? otherSeparators = Separators.Except(new[] { separator });
                    foreach (char otherSeparator in otherSeparators)
                    {
                        yield return new Mutation(
                            mutated: target.Replace(separator, otherSeparator),
                            original: target,
                            mutator: Kind,
                            reason: $"Separator Changed: {separator} => {otherSeparator}");
                    }
                }
            }
        }
    }
}