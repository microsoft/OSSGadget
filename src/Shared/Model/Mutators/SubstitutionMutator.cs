// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations for if a known substitution exists.
    /// e.g. js -> javascript and vice versa.
    /// </summary>
    /// <remarks>
    /// Currently depends on the MutatorOverrideFunction being populated in the constructor.
    /// </remarks>
    public class SubstitutionMutator : BaseMutator
    {
        public new string Mutator = "SWAPPED_WITH_SUBSTITUTE";

        public SubstitutionMutator(Func<string, string, IEnumerable<(string Name, string Reason)>>? func = null)
        {
            this.MutatorOverrideFunction = func;
        }

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            if (this.MutatorOverrideFunction != null)
            {
                return this.MutatorOverrideFunction(arg, Mutator);
            }

            return Array.Empty<(string Name, string Reason)>();
        }
    }
}