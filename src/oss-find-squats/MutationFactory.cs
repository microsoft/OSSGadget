// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class MutationFactory
    {
        private BaseProjectManager manager;

        public MutationFactory(BaseProjectManager manager)
        {
            this.manager = manager;
        }


        public Dictionary<string, IList<string>> Mutate(string name)
        {
            Dictionary<string, IList<string>> mutations = new();

            // do the mutating for the mutators on this package manager.
            // update this.mutations with the results of all mutations.
            var mutationsList = manager.Mutators.SelectMany(m => m.Generate(name));
            foreach (var (mutation, reason) in mutationsList)
            {
                if (mutations.ContainsKey(mutation))
                {
                    mutations[mutation].Add(reason);
                }
                else
                {
                    mutations[mutation] = new List<string> {reason};
                }
            }

            return mutations;
        }

    }
}