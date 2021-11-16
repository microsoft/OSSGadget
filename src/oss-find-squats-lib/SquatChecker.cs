// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Model.Mutators;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.CST.OpenSource.FindSquats
{
    /// <summary>
    /// The class to wrap around the mutators to get the dictionary of potential package mutations
    /// instead of printing them to the console as is done in <see cref="FindSquatsTool"/>.
    /// </summary>
    public class SquatChecker
    {
        /// <summary>
        /// The options for <see cref="SquatChecker"/>.
        /// </summary>
        public class Options
        {
            public int SleepDelay { get; set; }
        }

        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();


        /// <summary>
        /// Generate the mutations for the package.
        /// </summary>
        /// <returns>A dictionary of each mutation, with a list of the reasons why this mutation showed up.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the package doesn't have a name.</exception>
        public Dictionary<string, IList<string>> Mutate(PackageURL pUrl, BaseProjectManager manager)
        {
            Dictionary<string, IList<string>> mutations = new();

            // Go through each mutator in this package manager and generate the mutations.
            var mutationsList = manager.Mutators.SelectMany(m =>
                m.Generate(pUrl.Name ?? throw new InvalidOperationException()));
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
                    mutations[mutation] = new List<string> {reason};
                }
            }

            // Return the dictionary of all mutations.
            return mutations;
        }

        public async IAsyncEnumerable<FindSquatResult> CheckSquats(PackageURL purl, Options? options)
        {
            if (purl.Name is not null && purl.Type is not null)
            {
                var manager = ProjectManagerFactory.CreateProjectManager(purl, null) ??
                                 throw new InvalidOperationException();

                var mutationsDict = this.Mutate(purl, manager);

                foreach ((var candidate, var rules) in mutationsDict)
                {
                    if (options?.SleepDelay > 0)
                    {
                        Thread.Sleep(options.SleepDelay);
                    }

                    // Nuget will match "microsoft.cst.oat." against "Microsoft.CST.OAT" but these are the same package
                    // For nuget in particular we filter out this case
                    if (manager is NuGetProjectManager)
                    {
                        if (candidate.EndsWith('.'))
                        {
                            if (candidate.Equals($"{purl.Name}.", StringComparison.InvariantCultureIgnoreCase))
                            {
                                continue;
                            }
                        }
                    }

                    var candidatePurl = new PackageURL(purl.Type, candidate);
                    FindSquatResult? res = null;
                    try
                    {
                        var versions = await manager.EnumerateVersions(candidatePurl);

                        if (versions.Any())
                        {
                            res = new FindSquatResult(
                                packageName: candidate,
                                packageUrl: candidatePurl,
                                squattedPackage: purl,
                                rules: rules);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(
                            $"Could not enumerate versions. Package {candidate} likely doesn't exist. {e.Message}:{e.StackTrace}");
                    }

                    if (res is not null)
                    {
                        yield return res;
                    }
                }
            }
        }
    }
}