// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Helpers;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates ASCII homoglyphs.
    /// Similar looking letters were swapped out for others. eg. m -> n or r -> n.
    /// </summary>
    public class AsciiHomoglyphMutator : BaseMutator
    {
        public new string Mutator = "ASCII_HOMOGLYPH";

        private static Dictionary<char, string> homoglyphs = new()
        {
            ['a'] = "eoq4",
            ['b'] = "dp",
            ['c'] = "o",
            ['d'] = "bpq",
            ['e'] = "ao",
            ['f'] = "t",
            ['g'] = "q",
            ['h'] = "b",
            ['i'] = "lj",
            ['j'] = "il",
            ['l'] = "ij1",
            ['m'] = "n",
            ['n'] = "rmu",
            ['o'] = "ea0",
            ['p'] = "qg",
            ['q'] = "pg",
            ['r'] = "n",
            ['t'] = "f",
        };

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            // assumption is that attacker is making just one change
            for (int i = 0; i < arg.Length; i++)
            {
                if (homoglyphs.ContainsKey(arg[i]))
                {
                    foreach (var c in homoglyphs[arg[i]])
                    {
                        yield return (arg.ReplaceCharAtPosition(c, i), Mutator);
                    }
                }
            }
        }
    }
}