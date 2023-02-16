// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    using Microsoft.CST.OpenSource.Helpers;
    using System.Collections.Generic;

    /// <summary>
    /// Generates ASCII homoglyphs.
    /// Similar looking letters were swapped out for others. eg. m -> n or r -> n.
    /// </summary>
    public class AsciiHomoglyphMutator : IMutator
    {
        public MutatorType Kind { get; } = MutatorType.AsciiHomoglyph;

        private static readonly Dictionary<char, string> homoglyphs = new()
        {
            ['a'] = "eoq4",
            ['b'] = "dhp",
            ['c'] = "o",
            ['d'] = "bpq",
            ['e'] = "ao",
            ['f'] = "t",
            ['g'] = "pq",
            ['h'] = "b",
            ['i'] = "lj",
            ['j'] = "il",
            ['l'] = "ij1",
            ['m'] = "n",
            ['n'] = "rmu",
            ['o'] = "cea0",
            ['p'] = "bdqg",
            ['q'] = "adpg",
            ['r'] = "n",
            ['t'] = "f",
            ['u'] = "n",
            ['0'] = "o",
            ['1'] = "l",
            ['4'] = "a"
        };

        public IEnumerable<Mutation> Generate(string arg)
        {
            // assumption is that attacker is making just one change
            for (int i = 0; i < arg.Length; i++)
            {
                if (homoglyphs.ContainsKey(arg[i]))
                {
                    foreach (char c in homoglyphs[arg[i]])
                    {
                        yield return new Mutation(
                            mutated: arg.ReplaceCharAtPosition(c, i),
                            original: arg,
                            mutator: Kind,
                            reason: $"Ascii Homoglpyh: {arg[i]} => {c}");
                    }
                }
            }
        }
    }
}