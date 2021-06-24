// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using NLog;
using System;
using System.Collections.Generic;
using System.IO;
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
            var originalDiffResultsLength = diffResults.Count();
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
            strategyResult.NumIgnoredFiles += (originalDiffResultsLength - diffResults.Count());
            Helpers.AddDifferencesToStrategyResult(strategyResult, diffResults);
            
            diffResults = Helpers.DirectoryDifference(Options.SourceDirectory!, Options.PackageDirectory!, Options.DiffTechnique);
            originalDiffResultsLength = diffResults.Count();
            diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
            strategyResult.NumIgnoredFiles += (originalDiffResultsLength - diffResults.Count());
            Helpers.AddDifferencesToStrategyResult(strategyResult, diffResults, reverseDirection: true);

            return strategyResult;
        }
    }
}