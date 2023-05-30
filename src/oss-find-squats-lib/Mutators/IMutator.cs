// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Extensions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Web;

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
                if (mutation.Mutated.Length == 0)
                {
                    // Don't make mutations that are empty. i.e pkg:npm/ isn't valid.
                    continue;
                }
                
                if (mutation.Mutated.Length == 1 && !char.IsLetterOrDigit(mutation.Mutated[0]))
                {
                    // Don't make mutations that are just one separator. i.e pkg:npm/. isn't valid.
                    continue;
                }

                if (hasNamespace)
                {
                    yield return new Mutation(
                        mutated: arg.CreateWithNewNames(arg.Name, mutation.Mutated).ToString(),
                        original: arg.ToString(),
                        reason: mutation.Reason,
                        mutator: mutation.Mutator);
                }
                else
                {
                    yield return new Mutation(
                        mutated: arg.CreateWithNewNames(mutation.Mutated, arg.Namespace).ToString(),
                        original: arg.ToString(),
                        reason: mutation.Reason,
                        mutator: mutation.Mutator);
                }
            }
        }
    }
}