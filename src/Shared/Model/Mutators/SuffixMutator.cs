// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for if a suffix was added to the string.
    /// We check for these prefixes: ".", "x", "-", "X", "_".
    /// </summary>
    public class SuffixMutator : BaseMutator
    {
        public new string Mutator = "SUFFIX_ADDED";

        public SuffixMutator(Func<string, string, IEnumerable<(string Name, string Reason)>>? func = null)
        {
            this.MutatorOverrideFunction = func;
        }
        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            var suffixes = new[] { "s", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "ng", "-ng", ".", "x", "-", "_"};
            var mutations = suffixes.Select(s => (string.Concat(arg, s), Mutator));
            if (this.MutatorOverrideFunction != null)
            {
                return mutations.Concat(this.MutatorOverrideFunction(arg, Mutator));
            }
            return mutations;
        }
    }
}