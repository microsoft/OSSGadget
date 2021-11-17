// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a character was changed after a separator was used.
    /// NOTE: Doesn't currently support extra separators like the <see cref="SeparatorMutator"/> does.
    /// </summary>
    public class AfterSeparatorMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.AfterSeparator;

        public IEnumerable<Mutation> Generate(string arg)
        {
            foreach (var s in SeparatorMutator.separators)
            {
                var splits = arg.Split(s, StringSplitOptions.RemoveEmptyEntries);

                if (splits.Length == 2)
                {
                    for (var c = 'a'; c <= 'z'; c++)
                    {
                        yield return new Mutation(
                            mutated: splits[0] + s + c + splits[1][1..],
                            original: arg,
                            mutator: Kind,
                            reason: "After Separator");
                    }
                }
            }
        }
    }
}