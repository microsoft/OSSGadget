// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using NLog;
using System.IO;
using System.Linq;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    /// <summary>
    /// This strategy uses the Microsoft Oryx (github.com/Microsoft/oryx) Docker image to attempt to
    /// build the source repository. The priority high as this project attempts to create a runnable
    /// build, meaning, it will bring in ancillary packages that aren't included in the actual
    /// package itself.
    /// </summary>
    internal class OryxBuildStrategy : BaseStrategy
    {
        public override StrategyPriority PRIORITY => StrategyPriority.Low;

        public OryxBuildStrategy(StrategyOptions options) : base(options)
        {
        }

        /// <summary>
        /// Determines whether this strategy applies to the given package/source. For this strategy,
        /// we'll let Oryx do whatever it can.
        /// </summary>
        /// <returns></returns>
        public override bool StrategyApplies()
        {
            if (!GenericStrategyApplies(new[] { Options.SourceDirectory, Options.PackageDirectory }))
            {
                return false;
            }

            if (GetPathToCommand(new[] { "docker" }) == null)
            {
                Logger.Debug("Strategy {0} cannot be used, as Docker does not appear to be installed.", this.GetType().Name);
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

            var workingDirectory = Helpers.GetFirstNonSingularDirectory(Options.SourceDirectory);
            if (workingDirectory == null)
            {
                Logger.Warn("Unable to find correct source directory to run Oryx against. Unable to continue.");
                return null;
            }

            var outputDirectory = Path.Join(Options.TemporaryDirectory, "build");
            Directory.CreateDirectory(outputDirectory);
            var tempBuildArchiveDirectory = Path.Join(Options.TemporaryDirectory, "archive");
            Directory.CreateDirectory(tempBuildArchiveDirectory);

            var runResult = Helpers.RunCommand(workingDirectory, "docker", new[] {
                                           "run",
                                           "--rm",
                                           "--volume", $"{Path.GetFullPath(workingDirectory)}:/repo",
                                           "--volume", $"{Path.GetFullPath(outputDirectory)}:/build-output",
                                           "mcr.microsoft.com/oryx/build:latest",
                                           "oryx",
                                           "build",
                                           "/repo",
                                           "--output", "/build-output"
                                       }, out var stdout, out var stderr);

            var strategyResult = new StrategyResult()
            {
                Strategy = this.GetType()
            };

            if (runResult)
            {
                if (Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories).Any())
                {
                    if (Options.IncludeDiffoscope)
                    {
                        var diffoscopeTempDir = Path.Join(Options.TemporaryDirectory, "diffoscope");
                        var diffoscopeResults = GenerateDiffoscope(diffoscopeTempDir, outputDirectory, Options.PackageDirectory!);
                        strategyResult.Diffoscope = diffoscopeResults;
                    }

                    var diffResults = Helpers.DirectoryDifference(Options.PackageDirectory!, outputDirectory, Options.DiffTechnique);
                    var diffResultsOriginalCount = diffResults.Count();
                    diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
                    strategyResult.NumIgnoredFiles += (diffResultsOriginalCount - diffResults.Count());
                    Helpers.AddDifferencesToStrategyResult(strategyResult, diffResults);
                }
                else
                {
                    strategyResult.IsError = true;
                    strategyResult.Summary = "The OryxBuildStrategy did not complete successfully (no files produced).";
                }
            }
            else
            {
                strategyResult.IsError = true;
                strategyResult.Summary = "The OryxBuildStrategy did not complete successfully (container execution failed).";
            }

            return strategyResult;
        }
    }
}