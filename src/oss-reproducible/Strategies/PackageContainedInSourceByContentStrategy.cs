using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Reproducibility
{


    class PackageContainedInSourceByContentStrategy : BaseStrategy
    {
        public override StrategyPriority PRIORITY => StrategyPriority.Medium;

        public PackageContainedInSourceByContentStrategy(StrategyOptions options) : base(options)
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

            var numPackageFiles = Directory.EnumerateFiles(Options.PackageDirectory!, "*", SearchOption.AllDirectories).Count();

            var diffResults = Helpers.DirectoryDifference(Options.PackageDirectory!, Options.SourceDirectory!);
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
            Helpers.AddDifferencesToStrategyResult(strategyResult, diffResults);

            return strategyResult;
        }
    }
}
