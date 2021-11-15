// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for if a prefix was added to the string.
    /// We check for these prefixes: ".", "x", "-", "X", "_".
    /// </summary>
    public class PrefixMutator : BaseMutator
    {
        public new string Mutator = "PREFIX_ADDED";

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            var prefixes = new[] { ".", "x", "-", "X", "_" };
            return prefixes.Select(s => (string.Concat(s, arg), Mutator));
        }
    }
}