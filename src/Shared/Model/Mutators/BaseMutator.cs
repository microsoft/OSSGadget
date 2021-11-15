// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    public abstract class BaseMutator
    {
        public string Mutator = "BASE_MUTATOR";

        public Func<string, string, IEnumerable<(string Name, string Reason)>>? MutatorOverrideFunction = null;

        public abstract IEnumerable<(string Name, string Reason)> Generate(string arg);
    }
}