// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using NLog;
using System.IO;
using System.Linq;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    internal class PackageContainedInSourceStrategy : BaseStrategy
    {
        public override StrategyPriority PRIORITY => StrategyPriority.Medium;

        public PackageContainedInSourceStrategy(StrategyOptions options) : base(options)
        {
        }

        public override bool StrategyApplies()
        {
            return GenericStrategyApplies(new[] { Options.SourceDirectory, Options.PackageDirectory });
        }

        public override StrategyResult? Execute()
        {
            Logger.Debug("Executing {0} reproducibility strategy.", this.GetType().Name);
            if (!StrategyApplies())
            {
                Logger.Debug("Strategy does not apply, so cannot execute.");
                return null;
            }

            var strategyResult = new StrategyResult()
            {
                Strategy = this.GetType()
            };

            if (Options.IncludeDiffoscope)
            {
                var diffoscopeTempDir = Path.Join(Options.TemporaryDirectory, "diffoscope");
                var diffoscopeResults = GenerateDiffoscope(diffoscopeTempDir, Options.SourceDirectory!, Options.PackageDirectory!);
                strategyResult.Diffoscope = diffoscopeResults;
            }

            var diffResults = Helpers.DirectoryDifference(Options.PackageDirectory!, Options.SourceDirectory!, Options.DiffTechnique);
            var diffResultsOriginalCount = diffResults.Count();
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
            strategyResult.NumIgnoredFiles += (diffResultsOriginalCount - diffResults.Count());
            strategyResult.AddDifferencesToStrategyResult(diffResults);

            return strategyResult;
        }
    }
}