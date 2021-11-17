// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for if a character was duplicated in the string. Or duplicated and replaced.
    /// </summary>
    public class DuplicatorMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.Duplicator;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                yield return new Mutation()
                {
                    Mutated = string.Concat(arg[..i], arg[i], arg[i..]),
                    Original = arg,
                    Mutator = Kind,
                    Reason = "Letter Duplicated"
                };
            }

            for (int i = 0; i < arg.Length - 2; i++)
            {
                yield return new Mutation()
                {
                    Mutated = string.Concat(arg[..(i + 1)], arg[i], arg[(i + 2)..]),
                    Original = arg,
                    Mutator = Kind,
                    Reason = "Letter Duplicated and Replaced"
                };
            }

            yield return new Mutation()
            {
                Mutated = string.Concat(arg[..arg.Length], arg[^1]),
                Original = arg,
                Mutator = Kind,
                Reason = "Letter Duplicated and Replaced"
            };
        }
    }
}