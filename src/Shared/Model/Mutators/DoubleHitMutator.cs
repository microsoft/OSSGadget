// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for if a nearby character was double pressed.
    /// </summary>
    public class DoubleHitMutator : BaseMutator
    {
        public new string Mutator = "DOUBLE_HIT_CLOSE_LETTERS";

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = QwertyKeyboardHelper.GetNeighboringCharacters(arg[i]).ToList();

                foreach (var c in n)
                {
                    yield return (string.Concat(arg[..i], c, arg[i..]), Mutator);
                    yield return (string.Concat(arg[..(i + 1)], c, arg[(i + 1)..]), Mutator);
                }
            }
        }
    }
}