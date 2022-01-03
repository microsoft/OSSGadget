// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using NLog;
using System.IO;
using System.Linq;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    /// <summary>
    /// This strategy checks to see if the package content exactly matches the source code repository.
    /// </summary>
    internal class PackageMatchesSourceStrategy : BaseStrategy
    {
        public override StrategyPriority PRIORITY => StrategyPriority.High;

        public PackageMatchesSourceStrategy(StrategyOptions options) : base(options)
        {
        }

        public override bool StrategyApplies()
        {
            return GenericStrategyApplies(new[] { Options.SourceDirectory, Options.PackageDirectory });
        }

        public override StrategyResult? Execute()
        {
            Logger.Debug("Executing {0} reproducibility strategy.", GetType().Name);
            if (!StrategyApplies())
            {
                Logger.Debug("Strategy does not apply, so cannot execute.");
                return null;
            }

            StrategyResult? strategyResult = new StrategyResult()
            {
                Strategy = GetType()
            };

            if (Options.IncludeDiffoscope)
            {
                string? diffoscopeTempDir = Path.Join(Options.TemporaryDirectory, "diffoscope");
                string? diffoscopeResults = GenerateDiffoscope(diffoscopeTempDir, Options.SourceDirectory!, Options.PackageDirectory!);
                strategyResult.Diffoscope = diffoscopeResults;
            }

            System.Collections.Generic.IEnumerable<DirectoryDifference>? diffResults = OssReproducibleHelpers.DirectoryDifference(Options.PackageDirectory!, Options.SourceDirectory!, Options.DiffTechnique);
            int originalDiffResultsLength = diffResults.Count();
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, GetType().Name, d.Filename));
            strategyResult.NumIgnoredFiles += (originalDiffResultsLength - diffResults.Count());
            strategyResult.AddDifferencesToStrategyResult(diffResults);

            diffResults = OssReproducibleHelpers.DirectoryDifference(Options.SourceDirectory!, Options.PackageDirectory!, Options.DiffTechnique);
            originalDiffResultsLength = diffResults.Count();
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, GetType().Name, d.Filename));
            strategyResult.NumIgnoredFiles += (originalDiffResultsLength - diffResults.Count());
            strategyResult.AddDifferencesToStrategyResult(diffResults, reverseDirection: true);

            return strategyResult;
        }
    }
}