using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.CST.RecursiveExtractor;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    /// <summary>
    /// This strategy identifies packages as reproducible if running `npm pack` on the source repository produces a file
    /// that matches the content of the package downloaded from the registry.
    /// </summary>
    class AutoBuildProducesSamePackage : BaseStrategy
    {
        private static readonly Dictionary<string, string> DOCKER_CONTAINERS = new Dictionary<string, string>()
        {
            {"npm", "node:latest" },
            {"gem", "ruby:latest" },
            {"cpan", "perl:latest" },
            {"pypi", "python:latest" }
        };

        public override StrategyPriority PRIORITY => StrategyPriority.Medium;

        public AutoBuildProducesSamePackage(StrategyOptions options) : base(options)
        {
        }

        /// <summary>
        /// This strategy applies when the source and package directories exist, as well as if an autobuilder script is available.
        /// </summary>
        /// <returns></returns>
        public override bool StrategyApplies()
        {
            if (Options.SourceDirectory == null || Options.PackageDirectory == null)
            {
                Logger.Trace("Strategy {0} does not apply, as both source and package directories are required.", this.GetType().Name);
                return false;
            }

            if (!File.Exists(Path.Join("BuildHelperScripts", Options.PackageUrl?.Type, "autobuild.sh")))
            {
                Logger.Trace("Strategy {0} does not apply because no autobuilder script could be found.", this.GetType().Name);
                return false;
            }

            if (GetPathToCommand(new[] { "docker" }) == null)
            {
                Logger.Debug("Strategy {0} cannot be used, as Docker does not appear to be installed.", this.GetType().Name);
                return false;
            }

            if (!DOCKER_CONTAINERS.TryGetValue(Options.PackageUrl?.Type!, out string? dockerContainerName))
            {
                Logger.Debug("Strategy {0} does not apply because no docker container is known for type: {0}", Options.PackageUrl?.Type);
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
                Logger.Warn("Unable to find correct source directory to run `npm pack` from. Unable to continue.");
                return null;
            }
            var outputDirectory = Path.Join(Options.TemporaryDirectory, "build-output");

            var strategyResult = new StrategyResult()
            {
                Strategy = this.GetType()
            };

            var autoBuilderScript = Path.Join("/build-helpers", Options.PackageUrl?.Type, "autobuild.sh").Replace("\\", "/");
            var customPrebuild = GetCustomScript(Options.PackageUrl!, "prebuild")?.Replace("BuildHelperScripts/", "") ?? "";
            var customBuild = GetCustomScript(Options.PackageUrl!, "build")?.Replace("BuildHelperScripts/", "") ?? "";
            var customPostBuild = GetCustomScript(Options.PackageUrl!, "postbuild")?.Replace("BuildHelperScripts/", "") ?? "";
            if (!DOCKER_CONTAINERS.TryGetValue(Options.PackageUrl!.Type!, out string? dockerContainerName))
            {
                Logger.Debug("No docker container is known for type: {0}", Options.PackageUrl.Type);
                return null;
            }

            var runResult = RunCommand(workingDirectory, "docker", new[] {
                                            "run",
                                            "--rm",
                                            "--memory=4g",
                                            "--cpus=0.5",
                                            "--volume", $"{Path.GetFullPath(workingDirectory)}:/repo",
                                            "--volume", $"{Path.GetFullPath(outputDirectory)}:/build-output",
                                            "--volume", $"{Path.GetFullPath("BuildHelperScripts")}:/build-helpers",
                                            "--workdir=/repo",
                                            dockerContainerName,
                                            "bash",
                                            autoBuilderScript,
                                            customPrebuild,
                                            customBuild,
                                            customPostBuild
                                       }, out var stdout, out var stderr);

            if (runResult)
            {
                var packedFilenamePath = Path.Join(outputDirectory, "output.archive");
                if (!File.Exists(packedFilenamePath))
                {
                    Logger.Warn("Unable to find AutoBuilder archive.");
                    strategyResult.IsError = true;
                    strategyResult.Summary = "The AutoBuilder did not produce an output archive.";
                    return strategyResult;
                }

                var extractor = new Extractor();
                var packedDirectory = Path.Join(Options.TemporaryDirectory, "src_packed");
                extractor.ExtractToDirectoryAsync(packedDirectory, packedFilenamePath).Wait();

                var diffResults = Helpers.DirectoryDifference(Options.PackageDirectory!, packedDirectory);
                diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, this.GetType().Name, d.Filename));
                Helpers.AddDifferencesToStrategyResult(strategyResult, diffResults);
            }
            else
            {
                strategyResult.IsError = true;
                strategyResult.Summary = "The AutoBuilder did not complete successfully.";
            }
            
            return strategyResult;
        }

        internal static string? GetCustomScript(PackageURL packageUrl, string scriptType)
        {
            if (packageUrl == null || string.IsNullOrWhiteSpace(scriptType))
            {
                return null;
            }
            string? targetWithVersion;
            string? targetWithoutVersion;

            if (!string.IsNullOrEmpty(packageUrl.Namespace))
            {
                targetWithVersion = Path.Join("BuildHelperScripts", packageUrl.Type, packageUrl.Namespace, packageUrl.Name + "@" + packageUrl.Version + $".{scriptType}");
                targetWithoutVersion = Path.Join("BuildHelperScripts", packageUrl.Type, packageUrl.Namespace, packageUrl.Name + $".{scriptType}");
            }
            else
            {
                targetWithVersion = Path.Join("BuildHelperScripts", packageUrl.Type, packageUrl.Name + "@" + packageUrl.Version + $".{scriptType}");
                targetWithoutVersion = Path.Join("BuildHelperScripts", packageUrl.Type, packageUrl.Name + $".{scriptType}");
            }
            
            if (File.Exists(targetWithVersion))
            {
                return targetWithVersion.Replace("\\", "/");
            }
            if (File.Exists(targetWithoutVersion))
            {
                return targetWithoutVersion.Replace("\\", "/");
            }
            return null;
        }
    }
}
