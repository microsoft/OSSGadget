// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class FindSquats
    {
        public BaseProjectManager Manager;
        private PackageURL package;
        private MutationFactory _mutationFactory;

        public FindSquats(string type, string name)
        {
            this.package = new PackageURL(type, name);
            this.Manager = ProjectManagerFactory.CreateProjectManager(this.package, null) ??
                                 throw new InvalidOperationException();
            this._mutationFactory = new MutationFactory(this.Manager);
        }

        public Dictionary<string, IList<string>> Mutate()
        {
            var mutations = this._mutationFactory.Mutate(this.package.Name ?? throw new InvalidOperationException());
            return mutations;
        }
    }
}