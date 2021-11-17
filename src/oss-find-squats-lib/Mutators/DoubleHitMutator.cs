// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a nearby character was double pressed.
    /// </summary>
    public class DoubleHitMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.DoubleHit;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (var c in n)
                {
                    yield return new Mutation()
                    {
                        Mutated = string.Concat(arg[..i], c, arg[i..]),
                        Original = arg,
                        Mutator = Kind,
                        Reason = "Double hit character"
                    };
                    yield return new Mutation()
                    {
                        Mutated = string.Concat(arg[..(i + 1)], c, arg[(i + 1)..]),
                        Original = arg,
                        Mutator = Kind,
                        Reason = "Double hit character"
                    };
                }
            }
        }
    }
}