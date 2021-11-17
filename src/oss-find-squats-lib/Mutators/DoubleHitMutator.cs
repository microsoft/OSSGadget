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
        public string Name { get; } = "DOUBLE_HIT_CLOSE_LETTERS";

        public IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (var c in n)
                {
                    yield return (string.Concat(arg[..i], c, arg[i..]), Name);
                    yield return (string.Concat(arg[..(i + 1)], c, arg[(i + 1)..]), Name);
                }
            }
        }
    }
}