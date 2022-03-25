// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;

    /// <summary>
    /// Generates mutations for removing the namespace.
    /// </summary>
    public class RemoveNamespaceMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.RemovedNamespace;

        public IEnumerable<Mutation> Generate(string arg)
        {
            string[] packageSplit = arg.Split('/', 2);
            yield return new Mutation(
                    mutated: packageSplit[1], // Just the package name, no namespace.
                    original: arg,
                    mutator: Kind,
                    reason: $"Namespace removed: {packageSplit[0]}"); // The package's namespace that was removed.
        }
    }
}