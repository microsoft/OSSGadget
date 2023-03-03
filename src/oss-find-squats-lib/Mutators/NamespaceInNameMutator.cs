// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Extensions;
    using PackageUrl;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for moving the namespace to the name.
    /// </summary>
    public class NamespaceInNameMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.NamespaceInName;

        public HashSet<char> Separators { get; set; } = SeparatorRemovedMutator.DefaultSeparators.ToHashSet();

        public IEnumerable<Mutation> Generate(string arg)
        {
            PackageURL purl = new(arg);

            string namespaceStr = purl.Namespace.Replace("@", string.Empty).Replace("%40", string.Empty);

            foreach (var separator in Separators)
            {
                yield return new Mutation(
                    mutated: purl.CreateWithNewNames($"{namespaceStr}{separator}{purl.Name}", null).ToString(),
                    original: arg,
                    mutator: Kind,
                    reason: $"Namespace '{namespaceStr}' added to name with the separator: {separator}");
            }

            yield return new Mutation(
                    mutated: purl.CreateWithNewNames($"{namespaceStr}{purl.Name}", null).ToString(),
                    original: arg,
                    mutator: Kind,
                    reason: $"Namespace '{namespaceStr}' added to name with no separator");
        }

        public IEnumerable<Mutation> Generate(PackageURL arg)
        {
            if (!arg.HasNamespace())
            {
                yield break;
            }

            foreach (Mutation mutation in Generate(arg.ToString()))
            {
                yield return mutation;
            }
        }
    }
}