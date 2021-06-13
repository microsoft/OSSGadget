using Microsoft.CST.RecursiveExtractor;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SharpCompress;
using SharpCompress.Common;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives;

namespace Microsoft.CST.OpenSource.Reproducibility
{

    /// <summary>
    /// This strategy uses the Microsoft Oryx (github.com/Microsoft/oryx) Docker image to attempt to build
    /// the source repository. The priority high as this project attempts to create a runnable build, meaning,
    /// it will bring in ancillary packages that aren't included in the actual package itself.
    /// </summary>
    class OryxBuildStrategy : BaseStrategy
    {
        public override StrategyPriority PRIORITY => StrategyPriority.Low;

        public OryxBuildStrategy(StrategyOptions options) : base(options)
        {
        }

        /// <summary>
        /// Determines whether this strategy applies to the given package/source. For
        /// this strategy, we'll let Oryx do whatever it can.
        /// </summary>
        /// <returns></returns>
        public override bool StrategyApplies()
        {
            if (Options.SourceDirectory == null || Options.PackageDirectory == null)
            {
                Logger.Debug("Strategy {0} does not apply, as both source and package directories are required.", this.GetType().Name);
                return false;
            }
            if (GetPathToCommand("docker") == null)
            {
                Logger.Debug("Strategy {0} cannot be used, as Docker does not appear to be installed.", this.GetType().Name);
                return false;
            }
            return false;  // disabled
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
            
            var runResult = RunCommand(workingDirectory, "docker", new[] {
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

            // Zip up the output directory, then recursively extract
            var unpackedDirectory = Path.Join(Options.TemporaryDirectory, "unpacked");
            var tempBuildArchiveFile = Path.Join(tempBuildArchiveDirectory, "archive.zip");
            Directory.CreateDirectory(unpackedDirectory);
            CreateZipFromDirectory(outputDirectory, tempBuildArchiveFile);
            var extractor = new Extractor();
            extractor.ExtractToDirectoryAsync(unpackedDirectory, tempBuildArchiveFile).Wait();

            var targetPackageDirectory = Helpers.GetFirstNonSingularDirectory(Options.PackageDirectory);
            var targetPackedDirectory = Helpers.GetFirstNonSingularDirectory(unpackedDirectory);

            var diffResults = Helpers.GetDirectoryDifferenceByFilename(targetPackageDirectory, targetPackedDirectory, Options.PackageUrl, this.GetType().Name);
            if (!diffResults.Any())
            {
                strategyResult.Summary = "Successfully reproduced package.";
                strategyResult.IsSuccess = true;
                Logger.Debug("Strategy {0} succeeded. When running 'oryx build' on the source code repository, the results match the package contents.", this.GetType().Name);
            }
            else
            {
                strategyResult.Summary = "Failed to reproduce the package.";
                strategyResult.IsSuccess = false;

                foreach (var diffResult in diffResults)
                {
                    // We only care about files added to the package or modified from the repo. It's OK
                    // that there are files in the repo that aren't in the package.
                    if (diffResult.Operation == DirectoryDifferenceOperation.Added)
                    {
                        strategyResult.Messages.Add($"File [{diffResult.Filename}] exists in the package but was not produced during a build.");
                        Logger.Debug("  [+] {0}", diffResult.Filename);
                    }
                    else if (diffResult.Operation == DirectoryDifferenceOperation.Modified)
                    {
                        strategyResult.Messages.Add($"File [{diffResult.Filename}] has different contents between the package and the build.");
                        Logger.Debug("  [*] {0}", diffResult.Filename);
                    }
                }
                Logger.Debug("Strategy {0} failed. The 'oryx build' command did not generate the same package contents.", this.GetType().Name);
            }
            return strategyResult;
        }
    }
}
