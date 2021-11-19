// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Generates mutations for if a nearby character was double pressed.
    /// </summary>
    public class DoubleHitMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.DoubleHit;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                List<char> n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (char c in n)
                {
                    yield return new Mutation(
                        mutated: string.Concat(arg[..i], c, arg[i..]),
                        original: arg,
                        mutator: Kind,
                        reason: $"Double hit character: {c}");
                }
            }
        }
    }
}