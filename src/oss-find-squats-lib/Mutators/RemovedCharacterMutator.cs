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
        public string Name { get; } = "REMOVED_CHARACTER";

        public IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for(var i = 1; i < arg.Length; i++)
            {
                yield return ($"{arg[..i]}{arg[(i + 1)..]}", Name);
            }
        }
    }
}