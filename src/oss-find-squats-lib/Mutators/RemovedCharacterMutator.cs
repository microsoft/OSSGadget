// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for removing a character in the string.
    /// </summary>
    public class RemovedCharacterMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.RemovedCharacter;

        public IEnumerable<Mutation> Generate(string arg)
        {
            for (var i = 1; i < arg.Length; i++)
            {
                yield return new Mutation()
                {
                    Mutated = $"{arg[..i]}{arg[(i + 1)..]}",
                    Original = arg,
                    Mutator = Kind,
                    Reason = "Character Removed"
                };
            }
        }
    }
}