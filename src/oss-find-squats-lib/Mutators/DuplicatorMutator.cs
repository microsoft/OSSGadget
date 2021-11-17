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
        public string Name { get; } = "LETTER_DUPLICATED";

        public IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for(int i = 0; i < arg.Length; i++)
            {
                yield return (string.Concat(arg[..i], arg[i], arg[i..]), Name);
            }

            for (int i = 0; i < arg.Length - 2; i++)
            {
                yield return (string.Concat(arg[..(i+1)], arg[i], arg[(i+2)..]), Name + "_AND_REPLACED");
            }

            yield return (string.Concat(arg[..arg.Length], arg[^1]), Name + "_AND_REPLACED");
        }
    }
}