using Microsoft.CST.OpenSource.FindSquats.Mutators;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.FindSquats.ExtensionMethods
{
    public static class MutateExtension
    {
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        internal static IList<Mutator> BaseMutators { get; } = new List<Mutator>()
        {
            new AfterSeparatorMutator(),
            new AsciiHomoglyphMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new SeparatorMutator(),
            new SubstitutionMutator(),
            new SuffixMutator(),
            new SwapOrderOfLettersMutator(),
            new UnicodeHomoglyphMutator(),
            new VowelSwapMutator(),
        };

        internal static IList<Mutator> NugetMutators { get; } = new List<Mutator>()
        {
            new AfterSeparatorMutator(),
            new AsciiHomoglyphMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new SeparatorMutator(),
            new SubstitutionMutator(),
            new SuffixMutator(additionalSuffixes: new[] { "net", ".net", "nuget"}),
            new SwapOrderOfLettersMutator(),
            new VowelSwapMutator(),
        };

        internal static IList<Mutator> NpmMutators { get; } = new List<Mutator>()
        {
            new AfterSeparatorMutator(),
            new AsciiHomoglyphMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new SeparatorMutator(),
            new SubstitutionMutator(new List<(string Original, string Substitution)>()
            {
                ("js", "javascript"),
                ("ts", "typescript"),
            }),
            new SuffixMutator(additionalSuffixes: new[] { "js", ".js", "javascript", "ts", ".ts", "typescript"}),
            new SwapOrderOfLettersMutator(),
            new VowelSwapMutator(),
        };

        public static async IAsyncEnumerable<FindSquatResult> EnumerateSquats(this BaseProjectManager manager, PackageURL purl, MutateOptions options)
        {
            var mutationsToUse = manager switch
            {
                NuGetProjectManager => NugetMutators,
                NPMProjectManager => NpmMutators,
                _ => BaseMutators
            };
            await foreach(var mutation in manager.EnumerateSquats(purl, mutationsToUse, options))
            {
                yield return mutation;
            }
        }

        public static async IAsyncEnumerable<FindSquatResult> EnumerateSquats(this BaseProjectManager manager, PackageURL purl, IList<Mutator> mutators, MutateOptions options)
        {
            if (purl.Name is null || purl.Type is null)
            {
                yield break;
            }
            foreach(var mutator in mutators)
            {
                foreach((var candidate, var reason) in mutator.Generate(purl.Name))
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
                                rules: new string[] { reason });
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace($"Could not enumerate versions. Package {candidate} likely doesn't exist. {e.Message}:{e.StackTrace}");
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
