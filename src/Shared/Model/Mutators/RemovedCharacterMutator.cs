// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for removing a character in the string and continuing.
    /// </summary>
    public class RemovedCharacterMutator : BaseMutator
    {
        public new string Mutator = "REMOVED_CHARACTER";

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for(var i = 1; i < arg.Length; i++)
            {
                yield return ($"{arg[..i]}{arg[(i + 1)..]}", Mutator);
            }
        }
    }
}