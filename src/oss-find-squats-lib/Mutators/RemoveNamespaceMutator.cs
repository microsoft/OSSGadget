// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Extensions;
    using PackageUrl;
    using System.Collections.Generic;

    /// <summary>
    /// Generates mutations for removing the namespace.
    /// </summary>
    public class RemoveNamespaceMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.RemovedNamespace;

        public IEnumerable<Mutation> Generate(string arg)
        {
            PackageURL purl = new(arg);
            
            yield return new Mutation(
                    mutated: purl.CreateWithNewNames(purl.Name, null).ToString(), // Just the package name, no namespace.
                    original: arg,
                    mutator: Kind,
                    reason: $"Namespace removed: {purl.Namespace}"); // The package's namespace that was removed.
        }
    }
}