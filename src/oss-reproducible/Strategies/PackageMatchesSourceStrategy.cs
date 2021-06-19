// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using NLog;
using System.Linq;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    /// <summary>
    /// This strategy checks to see if the package content exactly matches the source code
    /// repository. It automatically excludes the .git directory.
    /// </summary>
    internal class PackageMatchesSourceStrategy : BaseStrategy
    {
        public override StrategyPriority PRIORITY => StrategyPriority.High;

        public PackageMatchesSourceStrategy(StrategyOptions options) : base(options)
        {
        }

        public override bool StrategyApplies()
        {
            if (Options.SourceDirectory == null || Options.PackageDirectory == null)
            {
                Logger.Debug("Strategy {0} does not apply, as both source and package directories are required.", this.GetType().Name);
                return false;
            }
            return true;
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

            var diffResults = Helpers.DirectoryDifference(Options.PackageDirectory!, Options.SourceDirectory!, Options.DiffTechnique);
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
            Helpers.AddDifferencesToStrategyResult(strategyResult, diffResults);

            return strategyResult;
        }
    }
}