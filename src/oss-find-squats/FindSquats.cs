// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.FindSquats
{
    /// <summary>
    /// The class to wrap around the mutators to get the dictionary of potential package mutations
    /// instead of printing them to the console as is done in <see cref="FindSquatsTool"/>.
    /// </summary>
    public class FindSquats
    {
        /// <summary>
        /// The project manager for the package we are finding squats for.
        /// </summary>
        public readonly BaseProjectManager Manager;

        /// <summary>
        /// The package to find squats for.
        /// </summary>
        private readonly PackageURL _package;

        /// <summary>
        /// Initializes a <see cref="FindSquats"/> instance.
        /// </summary>
        /// <param name="type">The type/manager of the package.</param>
        /// <param name="name">The name of the package.</param>
        /// <exception cref="InvalidOperationException">Thrown if a manager can't be created for the given package.</exception>
        public FindSquats(string type, string name)
        {
            this._package = new PackageURL(type, name);
            this.Manager = ProjectManagerFactory.CreateProjectManager(this._package, null) ??
                                 throw new InvalidOperationException();
        }

        /// <summary>
        /// Generate the mutations for the package that was created in the constructor.
        /// </summary>
        /// <returns>A dictionary of each mutation, with a list of the reasons why this mutation showed up.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the package doesn't have a name.</exception>
        public Dictionary<string, IList<string>> Mutate()
        {
            Dictionary<string, IList<string>> mutations = new();

            // Go through each mutator in this package manager and generate the mutations.
            var mutationsList = this.Manager.Mutators.SelectMany(m => m.Generate(this._package.Name ?? throw new InvalidOperationException()));
            foreach (var (mutation, reason) in mutationsList)
            {
                if (mutations.ContainsKey(mutation))
                {
                    // Add the new reason if this mutation has already been seen.
                    mutations[mutation].Add(reason);
                }
                else
                {
                    // Add the new mutation to the dictionary.
                    mutations[mutation] = new List<string> { reason };
                }
            }

            // Return the dictionary of all mutations.
            return mutations;
        }
    }
}