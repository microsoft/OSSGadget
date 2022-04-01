// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Extensions;
    using PackageUrl;
    using System.Collections.Generic;

    /// <summary>
    /// The mutator interface
    /// </summary>
    public interface IMutator
    {
        /// <summary>
        /// An enum specifying which kind of Mutator this is.
        /// </summary>
        public MutatorType Kind { get; }

        /// <summary>
        /// Generates the typo squat mutations for a string.
        /// </summary>
        /// <param name="arg">The string to generate mutations for.</param>
        /// <returns>A list of mutations with the name and reason.</returns>
        public IEnumerable<Mutation> Generate(string arg);

        /// <summary>
        /// Generates the typo squat mutations for a <see cref="PackageUrl"/>.
        /// </summary>
        /// <remarks>If the <paramref name="arg"/> has a namespace, the mutations will be done on the namespace, not the name.</remarks>
        /// <param name="arg">The <see cref="PackageURL"/> to generate mutations for.</param>
        /// <returns>A list of mutations with the name and reason.</returns>
        public IEnumerable<Mutation> Generate(PackageURL arg)
        {
            bool hasNamespace = arg.HasNamespace();
            foreach (Mutation mutation in Generate(hasNamespace ? arg.Namespace : arg.Name))
            {
                if (hasNamespace)
                {
                    yield return new Mutation(
                        mutated: arg.CreateWithNewNames(arg.Name, mutation.Mutated).ToString(),
                        original: mutation.Original,
                        reason: mutation.Reason,
                        mutator: mutation.Mutator);
                }

                yield return new Mutation(
                    mutated: arg.CreateWithNewNames(mutation.Mutated, arg.Namespace).ToString(),
                    original: mutation.Original,
                    reason: mutation.Reason,
                    mutator: mutation.Mutator);
            }
        }
    }
}