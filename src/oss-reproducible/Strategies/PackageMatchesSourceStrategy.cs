using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    /// <summary>
    /// This strategy checks to see if the package content exactly matches the source code repository.
    /// It automatically excludes the .git directory.
    /// </summary>
    class PackageMatchesSourceStrategy : BaseStrategy
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

            var strategyResult = new StrategyResult() {
                Strategy = this.GetType()
            };

            var diffResults = Helpers.GetDirectoryDifferenceByFilename(Options.SourceDirectory, Options.PackageDirectory, Options.PackageUrl, this.GetType().Name);
            if (diffResults.Any(s => s.Operation == DirectoryDifferenceOperation.Added || 
                                     s.Operation == DirectoryDifferenceOperation.Removed || 
                                     s.Operation == DirectoryDifferenceOperation.Modified))
            {
                Logger.Debug("Strategy [{0}] failed to reproduce package.", this.GetType().Name);
                foreach (var diffResult in diffResults)
                {
                    strategyResult.IsSuccess = false;
                    strategyResult.Summary = "Failed to reproduce package.";

                    // We only care about files added to the package or modified from the repo. It's OK
                    // that there are files in the repo that aren't in the package.
                    if (diffResult.Operation == DirectoryDifferenceOperation.Added)
                    {
                        strategyResult.Messages.Add($"File [{diffResult.Filename}] exists in the package but not in the source repository.");
                        Logger.Debug("  [+] {0}", diffResult.Filename);
                    }
                    else if (diffResult.Operation == DirectoryDifferenceOperation.Modified)
                    {
                        strategyResult.Messages.Add($"File [{diffResult.Filename}] was modified between the package and the source repository.");
                        Logger.Debug("  [*] {0}", diffResult.Filename);
                    }
                    else if (diffResult.Operation == DirectoryDifferenceOperation.Removed)
                    {
                        strategyResult.Messages.Add($"File [{diffResult.Filename}] exists in the source repository but not in the package.");
                        Logger.Debug("  [-] {0}", diffResult.Filename);
                    }
                }
            }
            else
            {
                strategyResult.IsSuccess = true;
                strategyResult.Summary = "Successfully reproduced the package.";
                Logger.Debug("Strategy [{0}] successfully reproduced the package.", this.GetType().Name);
            }

            return strategyResult;
        }
    }
}
