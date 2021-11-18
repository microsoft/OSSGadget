using Microsoft.CST.OpenSource.FindSquats.Mutators;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.CST.OpenSource.FindSquats.ExtensionMethods
{
    public static class MutateExtension
    {
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        internal static IEnumerable<Mutator> BaseMutators { get; } = new List<Mutator>()
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

        internal static IEnumerable<Mutator> NugetMutators { get; } = BaseMutators.Where(x => x is not UnicodeHomoglyphMutator and not SuffixMutator)
            .Append(new SuffixMutator(additionalSuffixes: new[] { "net", ".net", "nuget" }, skipSuffixes: new[] { "." }));

        internal static IEnumerable<Mutator> NpmMutators { get; } = BaseMutators.Where(x => x is not UnicodeHomoglyphMutator and not SuffixMutator)
            .Concat(new Mutator[]
                {
                    new SubstitutionMutator(new List<(string Original, string Substitution)>()
                    {
                        ("js", "javascript"),
                        ("ts", "typescript"),
                    }),
                    new SuffixMutator(additionalSuffixes: new[] { "js", ".js", "javascript", "ts", ".ts", "typescript"})
                });

        public static IEnumerable<Mutator> GetDefaultMutators(this BaseProjectManager manager) => manager switch
        {
            NuGetProjectManager => NugetMutators,
            NPMProjectManager => NpmMutators,
            _ => BaseMutators
        };

        public static async IAsyncEnumerable<FindSquatResult> EnumerateSquats(this BaseProjectManager manager, PackageURL purl, MutateOptions? options = null)
        {
            await foreach (FindSquatResult? mutation in manager.EnumerateSquats(purl, manager.GetDefaultMutators(), options))
            {
                yield return mutation;
            }
        }

        public static async IAsyncEnumerable<FindSquatResult> EnumerateSquats(this BaseProjectManager manager, PackageURL purl, IEnumerable<Mutator> mutators, MutateOptions? options = null)
        {
            if (purl.Name is null || purl.Type is null)
            {
                yield break;
            }

            HashSet<string> alreadyChecked = new();

            foreach (Mutator? mutator in mutators)
            {
                foreach (Mutation? mutation in mutator.Generate(purl.Name))
                {
                    if (!alreadyChecked.Add(mutation.Mutated))
                    {
                        Logger.Trace($"Already chcked {mutation.Mutated}. Skipping.");
                        continue;
                    }
                    if (options?.SleepDelay > 0)
                    {
                        Thread.Sleep(options.SleepDelay);
                    }
                    PackageURL candidatePurl = new(purl.Type, mutation.Mutated);
                    FindSquatResult? res = null;
                    try
                    {
                        IEnumerable<string>? versions = await manager.EnumerateVersions(candidatePurl);

                        if (versions.Any())
                        {
                            res = new FindSquatResult(
                                packageName: mutation.Mutated,
                                packageUrl: candidatePurl,
                                squattedPackage: purl,
                                mutations: new Mutation[] { mutation });
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace($"Could not enumerate versions. Package {mutation.Mutated} likely doesn't exist. {e.Message}:{e.StackTrace}");
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
