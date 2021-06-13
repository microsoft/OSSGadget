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
            if (diffResults.Any(s => s.Operation == DirectoryDifferenceOperation.Added || 
                                     s.Operation == DirectoryDifferenceOperation.Modified))
            {
                Logger.Debug("Strategy [{0}] failed to reproduce package.", this.GetType().Name);
                foreach (var diffResult in diffResults)
                {
                    strategyResult.Summary = "Failed to reproduce the package.";
                    strategyResult.IsSuccess = false;

                    // We only care about files added to the package or modified from the repo. It's OK
                    // that there are files in the repo that aren't in the package.
                    if (diffResult.Operation == DirectoryDifferenceOperation.Added)
                    {
                        strategyResult.Messages.Add($"File [{diffResult.Filename}] exists in the package but not in the source repository.");
                        Logger.Debug("  [+] {0}", diffResult.Filename);
                    }
                    else if (diffResult.Operation == DirectoryDifferenceOperation.Modified)
                    {
                        strategyResult.Messages.Add($"File [{diffResult.Filename}] has different contents between the package and the source repository.");
                        Logger.Debug("  [*] {0}", diffResult.Filename);
                    }
                }
            }
            else
            {
                strategyResult.Summary = "Successfully reproduced the package.";
                strategyResult.IsSuccess = true;
                Logger.Debug("Strategy [{0}] successfully reproduced the package.", this.GetType().Name);
            }

            return strategyResult;
        }
    }
}
