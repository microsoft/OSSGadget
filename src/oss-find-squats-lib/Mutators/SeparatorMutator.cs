// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations of added or removed separators.
    /// Separators are '.', '-', '_'.
    /// </summary>
    public class SeparatorMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.Separator;

        public static HashSet<char> separators = new() { '.', '-', '_' };

        /// <summary>
        /// Initializes a <see cref="SeparatorMutator"/> instance.
        /// Optionally takes in a additional separators, or a list of overriding separators to
        /// replace the default list with.
        /// </summary>
        /// <param name="additionalSeparators">An optional parameter for extra separators.</param>
        /// <param name="overrideSeparators">An optional parameter for list of separators to replace the
        /// default list with.</param>
        public SeparatorMutator(char[]? additionalSeparators = null, char[]? overrideSeparators = null)
        {
            if (overrideSeparators != null)
            {
                separators = new HashSet<char>(overrideSeparators);
            }
            if (additionalSeparators != null)
            {
                separators = new HashSet<char>(separators.Concat(additionalSeparators));
            }
        }

        public IEnumerable<Mutation> Generate(string arg)
        {
            foreach (var s in separators)
            {
                if (arg.Contains(s))
                {
                    var rest = separators.Except(new[] { s });
                    foreach (var r in rest)
                    {
                        yield return new Mutation(
                            mutated: arg.Replace(s, r),
                            original: arg,
                            mutator: Kind,
                            reason: "Separator Changed");
                    }

                    yield return new Mutation(
                        mutated: arg.Replace(s.ToString(), string.Empty),
                        original: arg,
                        mutator: Kind,
                        reason: "Separator Removed");
                }
            }
        }
    }
}