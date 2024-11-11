// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    using PackageUrl;

    /// <summary>
    /// This strategy identifies packages as reproducible if running `npm pack` on the source
    /// repository produces a file that matches the content of the package downloaded from the registry.
    /// </summary>
    internal class AutoBuildProducesSamePackage : BaseStrategy
    {
        private static readonly Dictionary<string, string> DOCKER_CONTAINERS = new()
        {
            { "npm", "node:latest" },
            { "gem", "ruby:latest" },
            { "cpan", "perl:latest" },
            { "pypi", "python:latest" },
            { "cargo", "rust:latest" }
        };

        public override StrategyPriority PRIORITY => StrategyPriority.Medium;

        public AutoBuildProducesSamePackage(StrategyOptions options) : base(options)
        {
        }

        /// <summary>
        /// This strategy applies when the source and package directories exist, as well as if an
        /// autobuilder script is available.
        /// </summary>
        /// <returns></returns>
        public override bool StrategyApplies()
        {
            if (!GenericStrategyApplies(new[] { Options.SourceDirectory, Options.PackageDirectory }))
            {
                return false;
            }

            if (!File.Exists(Path.Join("BuildHelperScripts", Options.PackageUrl?.Type, "autobuild.sh")))
            {
                Logger.Debug("Strategy {0} does not apply because no autobuilder script could be found.", GetType().Name);
                return false;
            }

            if (GetPathToCommand(new[] { "docker" }) == null)
            {
                Logger.Debug("Strategy {0} cannot be used, as Docker does not appear to be installed.", GetType().Name);
                return false;
            }

            if (!DOCKER_CONTAINERS.TryGetValue(Options.PackageUrl?.Type!, out _))
            {
                Logger.Debug("Strategy {0} does not apply because no docker container is known for type: {0}", Options.PackageUrl?.Type);
                return false;
            }
            return true;
        }

        public override StrategyResult? Execute()
        {
            Logger.Debug("Executing {0} reproducibility strategy.", GetType().Name);
            if (!StrategyApplies())
            {
                Logger.Debug("Strategy does not apply, so cannot execute.");
                return null;
            }

            string? workingDirectory = OssReproducibleHelpers.GetFirstNonSingularDirectory(Options.SourceDirectory);
            if (workingDirectory == null)
            {
                Logger.Warn("Unable to find correct source directory to run `npm pack` from. Unable to continue.");
                return null;
            }
            string? outputDirectory = Path.Join(Options.TemporaryDirectory, "build-output");

            StrategyResult? strategyResult = new StrategyResult()
            {
                Strategy = GetType()
            };

            string? autoBuilderScript = Path.Join("/build-helpers", Options.PackageUrl?.Type, "autobuild.sh").Replace("\\", "/");
            string? customPrebuild = GetCustomScript(Options.PackageUrl!, "prebuild")?.Replace("BuildHelperScripts/", "") ?? "";
            string? customBuild = GetCustomScript(Options.PackageUrl!, "build")?.Replace("BuildHelperScripts/", "") ?? "";
            string? customPostBuild = GetCustomScript(Options.PackageUrl!, "postbuild")?.Replace("BuildHelperScripts/", "") ?? "";
            if (!DOCKER_CONTAINERS.TryGetValue(Options.PackageUrl!.Type!, out string? dockerContainerName))
            {
                Logger.Debug("No docker container is known for type: {0}", Options.PackageUrl.Type);
                return null;
            }

            bool runResult = OssReproducibleHelpers.RunCommand(workingDirectory, "docker", new[] {
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
                                       }, out string? stdout, out string? stderr);
            if (runResult)
            {
                string? packedFilenamePath = Path.Join(outputDirectory, "output.archive");
                if (!File.Exists(packedFilenamePath))
                {
                    Logger.Warn("Unable to find AutoBuilder archive.");
                    strategyResult.IsError = true;
                    strategyResult.Summary = "The AutoBuilder did not produce an output archive.";
                    return strategyResult;
                }

                Extractor? extractor = new Extractor();
                string? packedDirectory = Path.Join(Options.TemporaryDirectory, "src_packed");
                extractor.ExtractToDirectoryAsync(packedDirectory, packedFilenamePath).Wait();

                if (Options.IncludeDiffoscope)
                {
                    string? diffoscopeTempDir = Path.Join(Options.TemporaryDirectory, "diffoscope");
                    string? diffoscopeResults = GenerateDiffoscope(diffoscopeTempDir, packedDirectory, Options.PackageDirectory!);
                    strategyResult.Diffoscope = diffoscopeResults;
                }

                IEnumerable<DirectoryDifference>? diffResults = OssReproducibleHelpers.DirectoryDifference(Options.PackageDirectory!, packedDirectory, Options.DiffTechnique);
                int diffResultsOriginalCount = diffResults.Count();
                diffResults = diffResults.Where(d => !IgnoreFilter.IsIgnored(Options.PackageUrl, GetType().Name, d.Filename));
                strategyResult.NumIgnoredFiles += (diffResultsOriginalCount - diffResults.Count());
                strategyResult.AddDifferencesToStrategyResult(diffResults);
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
            Func<string, string> normalize = (s =>
            {
                return s.Replace("%40", "@").Replace("%2F", "/").Replace("%2f", "/");
            });

            targetWithVersion = normalize(targetWithVersion);
            targetWithoutVersion = normalize(targetWithoutVersion);

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