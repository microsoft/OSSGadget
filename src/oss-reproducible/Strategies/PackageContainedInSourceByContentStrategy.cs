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
            var diffResults = Helpers.GetDirectoryDifferenceByContent(Options.PackageUrl!, Options.SourceDirectory, Options.PackageDirectory, this.GetType().Name);
            var numDifferences = diffResults.Count(s => s.Operation == DirectoryDifferenceOperation.Added);
            if (diffResults.Any(s => s.Operation == DirectoryDifferenceOperation.Added))
            {
                var alignment = 100 * (numPackageFiles - numDifferences) / numPackageFiles;

                strategyResult.Summary = $"Failed to reproduce package ({alignment}% aligned).";
                strategyResult.IsSuccess = false;
                Logger.Debug("Strategy [{0}] failed to reproduce package ({1}% alignment)", this.GetType().Name, alignment);
                
                foreach (var diffResult in diffResults)
                {
                    
                    // Since the strategy is based on hash matching, there's no 'modification' possible, we only care about adds.
                    if (diffResult.Operation == DirectoryDifferenceOperation.Added)
                    {
                        foreach (var f in diffResult.Filename.Split(','))
                        {
                            var fRelative = f[(1 + Options.TemporaryDirectory!.Length)..].Replace("\\", "/");
                            strategyResult.Messages.Add($"File [{fRelative}] exists in package but not in source directory (or has different contents).");
                            Logger.Debug("  [+] {0}", fRelative);
                        }
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
