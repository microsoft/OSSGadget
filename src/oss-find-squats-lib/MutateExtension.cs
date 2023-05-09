// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.ExtensionMethods
{
    using Contracts;
    using Extensions;
    using Microsoft.CST.OpenSource.FindSquats.Mutators;
    using Microsoft.CST.OpenSource.PackageManagers;
    using PackageUrl;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// This class contains extension methods to find potentially squatted packages in BaseProjectManager derived classes.
    /// </summary>
    public static class MutateExtension
    {
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The default set of mutators for an arbitrary BaseProjectManager.
        /// </summary>
        internal static IEnumerable<IMutator> BaseMutators { get; } = new List<IMutator>()
        {
            new AddCharacterMutator(),
            new AddContainingCharacterMutator(),
            new AsciiHomoglyphMutator(),
            new BitFlipMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new NamespaceInNameMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new RemoveNamespaceMutator(),
            new RemoveSeparatedSectionMutator(),
            new ReplaceCharacterMutator(),
            new SeparatorChangedMutator(),
            new SeparatorRemovedMutator(),
            new SuffixMutator(),
            new SwapOrderOfLettersMutator(),
            new UnicodeHomoglyphMutator(),
            new VowelSwapMutator()
        };

        /// <summary>
        /// Common variations known uniquely for Nuget/C#. Excludes '.' because Nuget will match "Microsoft.CST.OAT." against "Microsoft.CST.OAT", giving false positives for the same package.
        /// </summary>
        internal static IEnumerable<IMutator> NugetMutators { get; } = BaseMutators.Where(x => 
                x is not UnicodeHomoglyphMutator and not SuffixMutator and not CloseLettersMutator and not DoubleHitMutator)
            .Concat(new IMutator[]
                {
                    new CloseLettersMutator(additionalExcludedChars: new[] { '/' }),
                    new DoubleHitMutator(additionalExcludedChars: new[] { '/' }),
                    new SuffixMutator(additionalSuffixes: new[] { "net", ".net", "nuget", "core", ".core" }, skipSuffixes: new[] { "." })
                });

        /// <summary>
        /// Common variations known uniquely for NPM/Javascript. 
        /// </summary>
        internal static IEnumerable<IMutator> NpmMutators { get; } = BaseMutators.Where(x => x is not UnicodeHomoglyphMutator and not PrefixMutator and not SuffixMutator and not RemoveSeparatedSectionMutator)
            .Concat(new IMutator[]
                {
                    new SubstitutionMutator(new List<(string Original, string Substitution)>()
                    {
                        ("js", "javascript"),
                        ("ts", "typescript"),
                    }),
                    new PrefixMutator(additionalPrefixes: new[] { "js", "js-", "node", "node-","ts", "ts-"}),
                    new SuffixMutator(additionalSuffixes: new[] { "js", ".js", "-js", "javascript", "-javascript", "ts", ".ts", "-ts", "typescript", "-typescript", "node", "-node"})
                });
                
        /// <summary>
        /// Common variations known uniquely for Python. 
        /// </summary>
        internal static IEnumerable<IMutator> PythonMutators { get; } = BaseMutators.Where(x => 
                x is not UnicodeHomoglyphMutator and not SuffixMutator and not CloseLettersMutator and not DoubleHitMutator and not SeparatorChangedMutator)
            .Concat(new IMutator[]
            {
                new CloseLettersMutator(additionalExcludedChars: new[] { '/' }),
                new DoubleHitMutator(additionalExcludedChars: new[] { '/' }),
                new SeparatorChangedMutator(overrideSeparators: new[] { '.', '-' }),
                new SubstitutionMutator(new List<(string Original, string Substitution)>()
                {
                    ("py", "python"),
                }),
                new SuffixMutator(additionalSuffixes: new[] { "py", ".py", "-py", "python", "-python"})
            });

        /// <summary>
        /// Gets the default set of mutators for a given BaseProjectManager based on its Type.
        /// </summary>
        /// <param name="manager"></param>
        /// <returns>An IEnumerable of the recommended IMutators.</returns>
        public static IEnumerable<IMutator> GetDefaultMutators(this IBaseProjectManager manager) => manager switch
        {
            NuGetProjectManager => NugetMutators,
            NPMProjectManager => NpmMutators,
            PyPIProjectManager => PythonMutators,
            _ => BaseMutators
        };

        /// <summary>
        /// Generates mutations of the provided <see cref="PackageURL"/>
        /// with the mutators from <see cref="GetDefaultMutators(BaseProjectManager)"/>.
        /// </summary>
        /// <param name="manager">The ProjectManager to use for generating mutations.</param>
        /// <param name="purl">The Target package to check for squats.</param>
        /// <param name="options">The options for enumerating squats.</param>
        /// <returns>An <see cref="IDictionary{T, V}"/> where the key is the mutated name, and the value is a <see cref="IList{Mutation}"/> representing each candidate squat.</returns>
        public static IDictionary<string, IList<Mutation>>? EnumerateSquatCandidates(this IBaseProjectManager manager, PackageURL purl, MutateOptions? options = null)
        {
            return manager.EnumerateSquatCandidates(purl, manager.GetDefaultMutators(), options);
        }

        /// <summary>
        /// Generates <see cref="Mutation"/>s of the provided <see cref="PackageURL"/> with the provided <see cref="IEnumerable{IMutator}"/>.
        /// </summary>
        /// <remarks>If the <paramref name="purl"/> has a namespace, the mutations will be done on the namespace, not the name.</remarks>
        /// <param name="manager">The ProjectManager to generate the mutations.</param>
        /// <param name="purl">The Target package to check for squats.</param>
        /// <param name="mutators">The mutators to use. Will ignore the default set of mutators.</param>
        /// <param name="options">The options for enumerating squats.</param>
        /// <returns>An <see cref="IDictionary{T, V}"/> where the key is the mutated name, and the value is a <see cref="IList{Mutation}"/> representing each candidate squat.</returns>
        public static IDictionary<string, IList<Mutation>>? EnumerateSquatCandidates(this IBaseProjectManager manager, PackageURL purl, IEnumerable<IMutator> mutators, MutateOptions? options = null)
        {
            if (purl.Name is null || purl.Type is null)
            {
                return null;
            }

            ConcurrentDictionary<string, IList<Mutation>> generatedMutations = new();

            foreach (IMutator mutator in mutators)
            {
                // If the mutator is in the list of mutators to exclude in the options, then skip.
                if (options is { ExcludedMutators: { } } && options.ExcludedMutators.Contains(mutator.Kind))
                {
                    continue;
                }

                foreach (Mutation mutation in mutator.Generate(purl))
                {
                    generatedMutations.AddOrUpdate(mutation.Mutated, new List<Mutation>() { mutation }, (_, list) =>
                        {
                            list.Add(mutation);
                            return list;
                        });
                }
            }

            return generatedMutations;
        }

        /// <summary>
        /// Asynchronously enumerates existing packages that exist from the <see cref="IDictionary{T, D}"/> of each candidate.
        /// Use <see cref="EnumerateSquatCandidates(BaseProjectManager, PackageURL, IEnumerable{IMutator}, MutateOptions?)"/> to create the dictionary of <paramref name="candidateMutations"/>.
        /// If <paramref name="candidateMutations"/> is null, will automatically generate the candidates using <see cref="EnumerateSquatCandidates(BaseProjectManager, PackageURL, IEnumerable{IMutator}, MutateOptions?)"/>.
        /// </summary>
        /// <param name="manager">The ProjectManager to use for checking the generated mutations.</param>
        /// <param name="purl">The Target package to check for squats.</param>
        /// <param name="candidateMutations">The <see cref="IList{Mutation}"/> representing each squatting candidate.</param>
        /// <param name="options">The options for enumerating through existing squats.</param>
        /// <returns>An <see cref="IAsyncEnumerable{FindPackageSquatResult}"/> with the packages that exist which match one of the <paramref name="candidateMutations"/>.</returns>
        public static async IAsyncEnumerable<FindPackageSquatResult> EnumerateExistingSquatsAsync(this IBaseProjectManager manager, PackageURL purl, IDictionary<string, IList<Mutation>>? candidateMutations, MutateOptions? options = null)
        {
            if (purl.Name is null || purl.Type is null)
            {
                yield break;
            }

            if (candidateMutations is null)
            {
                candidateMutations = manager.EnumerateSquatCandidates(purl, options);
            }

            if (candidateMutations is not null)
            {
                foreach ((string candidatePurlString, IList<Mutation> mutations) in candidateMutations)
                {
                    if (options?.SleepDelay > 0)
                    {
                        Thread.Sleep(options.SleepDelay);
                    }

                    // Create the purl from the mutation to see if it exists.
                    PackageURL candidatePurl = new(candidatePurlString);
                    FindPackageSquatResult? res = null;
                    try
                    {
                        if (await manager.PackageExistsAsync(candidatePurl, options?.UseCache ?? true))
                        {
                            // The candidate mutation exists on the package registry.
                            res = new FindPackageSquatResult(
                                mutatedPackageName: candidatePurl.GetFullName(),
                                mutatedPackageUrl: candidatePurl,
                                originalPackageUrl: purl,
                                mutations: mutations);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace($"Could not determine if package exists. Package {candidatePurlString} likely doesn't exist. {e.Message}:{e.StackTrace}");
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
