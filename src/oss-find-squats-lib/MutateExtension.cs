﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.ExtensionMethods
{
    using Microsoft.CST.OpenSource.FindSquats.Mutators;
    using Microsoft.CST.OpenSource.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// This class contains extension methods to find potentially squatted packages in BaseProjectManager derived classes.
    /// </summary>
    public static class MutateExtension
    {
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        internal static IEnumerable<IMutator> BaseMutators { get; } = new List<IMutator>()
        {
            new AfterSeparatorMutator(),
            new AsciiHomoglyphMutator(),
            new BitFlipMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new SeparatorMutator(),
            new SuffixMutator(),
            new SwapOrderOfLettersMutator(),
            new UnicodeHomoglyphMutator(),
            new VowelSwapMutator()
        };

        internal static IEnumerable<IMutator> NugetMutators { get; } = BaseMutators.Where(x => x is not UnicodeHomoglyphMutator and not SuffixMutator)
            .Concat(new IMutator[]
                {
                    new SuffixMutator(additionalSuffixes: new[] { "net", ".net", "nuget" }, skipSuffixes: new[] { "." })
                });

        internal static IEnumerable<IMutator> NpmMutators { get; } = BaseMutators.Where(x => x is not UnicodeHomoglyphMutator and not SuffixMutator)
                .Concat(new IMutator[]
                    {
                    new SubstitutionMutator(new List<(string Original, string Substitution)>()
                    {
                        ("js", "javascript"),
                        ("ts", "typescript"),
                    }),
                    new SuffixMutator(additionalSuffixes: new[] { "js", ".js", "javascript", "ts", ".ts", "typescript"})
                    });

        /// <summary>
        /// Gets the default set of mutators for a given BaseProjectManager based on its Type.
        /// </summary>
        /// <param name="manager"></param>
        /// <returns>An IEnumerable of the recommended IMutators.</returns>
        public static IEnumerable<IMutator> GetDefaultMutators(this BaseProjectManager manager) => manager switch
        {
            NuGetProjectManager => NugetMutators,
            NPMProjectManager => NpmMutators,
            _ => BaseMutators
        };

        /// <summary>
        /// Asynchronously Enumerate Squats for a given PackageURL with the default IMutators for this ProjectManager.
        /// </summary>
        /// <param name="manager">The ProjectManager to use for checking the generated mutations.</param>
        /// <param name="purl">The Target package to check for squats.</param>
        /// <param name="options">The options for enumerating squats.</param>
        /// <returns>An IAsyncEnumerable of FindPackageSquatResult objects representing each candidate squat found to exist in the provided manager.</returns>
        public static async IAsyncEnumerable<FindPackageSquatResult> EnumerateSquats(this BaseProjectManager manager, PackageURL purl, MutateOptions? options = null)
        {
            await foreach (FindPackageSquatResult? mutation in manager.EnumerateSquats(purl, manager.GetDefaultMutators(), options))
            {
                yield return mutation;
            }
        }

        /// <summary>
        /// Asynchronously Enumerate Squats for a given PackageURL.
        /// </summary>
        /// <param name="manager">The ProjectManager to use for checking the generated mutations.</param>
        /// <param name="purl">The Target package to check for squats.</param>
        /// <param name="options">The options for enumerating squats.</param>
        /// <param name="mutators">The mutators to use. Will ignore the default set of mutators.</param>
        /// <param name="options">The options for enumerating squats.</param>
        /// <returns>An IAsyncEnumerable of FindPackageSquatResult objects representing each candidate squat found to exist in the provided manager.</returns>
        public static async IAsyncEnumerable<FindPackageSquatResult> EnumerateSquats(this BaseProjectManager manager, PackageURL purl, IEnumerable<IMutator> mutators, MutateOptions? options = null)
        {
            if (purl.Name is null || purl.Type is null)
            {
                yield break;
            }

            Dictionary<string, IList<Mutation>> generatedMutations = new();

            foreach (IMutator? mutator in mutators)
            {
                foreach (Mutation? mutation in mutator.Generate(purl.Name))
                {
                    if (generatedMutations.ContainsKey(mutation.Mutated))
                    {
                        generatedMutations[mutation.Mutated].Add(mutation);
                    }
                    else
                    {
                        generatedMutations[mutation.Mutated] = new List<Mutation>() { mutation };
                    }
                }
            }

            foreach (KeyValuePair<string, IList<Mutation>> mutationSet in generatedMutations)
            {
                if (options?.SleepDelay > 0)
                {
                    Thread.Sleep(options.SleepDelay);
                }
                PackageURL candidatePurl = new(purl.Type, mutationSet.Key);
                FindPackageSquatResult? res = null;
                try
                {
                    if (await manager.PackageExists(candidatePurl))
                    {
                        res = new FindPackageSquatResult(
                            packageName: mutationSet.Key,
                            packageUrl: candidatePurl,
                            squattedPackage: purl,
                            mutations: mutationSet.Value);
                    }
                }
                catch (Exception e)
                {
                    Logger.Trace($"Could not check if package exists. Package {mutationSet.Key} likely doesn't exist. {e.Message}:{e.StackTrace}");
                }
                if (res is not null)
                {
                    yield return res;
                }
            }
        }
    }
}
