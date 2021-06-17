using DiffPlex.DiffBuilder.Model;
using DiffPlex.Model;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    /// <summary>
    /// This strategy checks to see if all files included in the package are also included in the source directory,
    /// and that they have identical contents. If a file was renamed, for example, it fails this strategy.
    /// </summary>
    class PackageContainedInSourceByFilenameStrategy : BaseStrategy
    {
        public override StrategyPriority PRIORITY => StrategyPriority.Medium;

        public PackageContainedInSourceByFilenameStrategy(StrategyOptions options) : base(options)
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

            var diffResults = Helpers.GetDirectoryDifferenceByFilename(Options!.SourceDirectory, Options!.PackageDirectory, Options.PackageUrl, this.GetType().Name);
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
            Helpers.AddDifferencesToStrategyResult(strategyResult, diffResults);

            return strategyResult;
        }
    }
}
