﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Shared;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    public enum StrategyPriority
    {
        None = 0,
        Low = 10,
        Medium = 20,
        High = 30
    }

    public class StrategyOptions
    {
        public PackageURL? PackageUrl { get; set; }
        public string? SourceDirectory { get; set; }
        public string? PackageDirectory { get; set; }
        public string? TemporaryDirectory { get; set; }
        public DiffTechnique DiffTechnique { get; set; } = DiffTechnique.Normalized;
        public bool IncludeDiffoscope { get; set; } = false;
    }

    public class StrategyResultMessage
    {
        public StrategyResultMessage()
        {
            this.Text = "";
            this.Filename = "";
        }

        public string Text { get; set; }
        public string? Filename { get; set; }
        public string? CompareFilename { get; set; }
        public IEnumerable<DiffPiece>? Differences { get; set; }
    }

    public class StrategyResult
    {
        public StrategyResult()
        {
            Messages = new HashSet<StrategyResultMessage>();
        }

        [JsonIgnore]
        public Type? Strategy { get; set; }

        public string? StrategyName { get => Strategy?.Name; }
        public string? Summary { get; set; }
        public HashSet<StrategyResultMessage> Messages;
        public bool IsSuccess { get; set; } = false;
        public bool IsError { get; set; } = false;
        public int NumIgnoredFiles { get; set; } = 0;
        public string? Diffoscope { get; set; }
    }

    public abstract class BaseStrategy
    {
        protected StrategyOptions Options;

        public virtual StrategyPriority PRIORITY => StrategyPriority.None;

        /// <summary>
        /// Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public abstract bool StrategyApplies();

        public abstract StrategyResult? Execute();

        public BaseStrategy(StrategyOptions options)
        {
            this.Options = options;
        }

        /// <summary>
        /// Checks the directories passed in, ensuring they aren't null, exist, and aren't empty.
        /// </summary>
        /// <param name="directories">Directories to check</param>
        /// <returns>True if the satisfy the above conditions, else false.</returns>
        public bool GenericStrategyApplies(IEnumerable<string?> directories)
        {
            if (directories == null)
            {
                Logger.Debug("Strategy {0} does not apply as no directories checked.", this.GetType().Name);
                return false;
            }

            bool result = true;
            foreach (var directory in directories)
            {
                if (directory == null)
                {
                    Logger.Debug("Strategy {0} does not apply as no directories checked.", this.GetType().Name);
                    result = false;
                }
                else if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Logger.Debug("Strategy {0} does not apply as {1} was empty.", this.GetType().Name, directory);
                    result = false;
                }
            }

            return result;
        }

        /// <summary>
        /// Locates all strategies (meaning, classes derived from BaseStrategy).
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Type>? GetStrategies(StrategyOptions strategyOptions)
        {
            var strategies = typeof(BaseStrategy).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseStrategy))).ToList();

            strategies.Sort((a, b) =>
            {
                if (a == b)
                {
                    return 0;
                }
                var aCtor = a.GetConstructor(new Type[] { typeof(StrategyOptions) });
                var aObj = aCtor?.Invoke(new object?[] { strategyOptions }) as BaseStrategy;

                var bCtor = b.GetConstructor(new Type[] { typeof(StrategyOptions) });
                var bObj = bCtor?.Invoke(new object?[] { strategyOptions }) as BaseStrategy;

                if (aObj == null && bObj != null) return -1;
                if (aObj != null && bObj == null) return 1;
                if (aObj != null && bObj != null)
                {
                    return aObj.PRIORITY.CompareTo(bObj.PRIORITY);
                }
                return 0;
            });
            strategies.Reverse();   // We want high priority to go first

            return strategies;
        }

        protected static string? GetPathToCommand(IEnumerable<string> commands)
        {
            foreach (var command in commands)
            {
                string[]? pathParts = null;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    pathParts = Environment.GetEnvironmentVariable("PATH")?.Split(';');
                }
                else
                {
                    pathParts = Environment.GetEnvironmentVariable("PATH")?.Split(':');
                }

                foreach (var pathPart in pathParts ?? Array.Empty<string>())
                {
                    var target = Path.Combine(pathPart, command);
                    if (File.Exists(target))
                    {
                        return target;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// This is a failure-resistent version of .CreateFromDirectory or .AddDirectory, both of
        /// which fail under various conditions. This function continues on (emitting a message to
        /// the logger) and returns false on any error.
        /// </summary>
        /// <param name="directoryName">Directory to zip</param>
        /// <param name="archiveName">File to write to.</param>
        /// <returns>true iff no errors occur</returns>
        protected static bool CreateZipFromDirectory(string directoryName, string archiveName)
        {
            var result = true;
            // Note that we're not using something like .CreateFromDirectory, or .AddDirectory,
            // since both of these had problems with permissions. Instead, we'll try to add each
            // file separately, and continue on any failures.
            using (var archive = ZipArchive.Create())
            {
                using (archive.PauseEntryRebuilding())
                {
                    foreach (var path in Directory.EnumerateFiles(directoryName, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(path);
                            archive.AddEntry(path[directoryName.Length..], fileInfo.OpenRead(), true, fileInfo.Length, fileInfo.LastWriteTime);
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug("Unable to add {0} to archive: {1}", path, ex.Message);
                            result = false;
                        }
                    }
                    archive.SaveTo(archiveName, CompressionType.Deflate);
                }
            }
            return result;
        }

        internal static string? GenerateDiffoscope(string workingDirectory, string leftDirectory, string rightDirectory)
        {
            Logger.Debug("Running Diffoscope on ({0}, {1})", leftDirectory, rightDirectory);
            Directory.CreateDirectory(workingDirectory);

            var runResult = Helpers.RunCommand(workingDirectory, "docker", new[] {
                                            "run",
                                            "--rm",
                                            "--memory=1g",
                                            "--cpus=0.5",
                                            "--volume", $"{Path.GetFullPath(leftDirectory)}:/work/left:ro",
                                            "--volume", $"{Path.GetFullPath(rightDirectory)}:/work/right:ro",
                                            "--volume", $"{Path.GetFullPath(workingDirectory)}:/work/output",
                                            "--workdir=/work",
                                            "registry.salsa.debian.org/reproducible-builds/diffoscope",
                                            "--html",
                                            "/work/output/results.html",
                                            "/work/left",
                                            "/work/right"
                                       }, out var stdout, out var stderr);

            var resultsFile = Path.Join(workingDirectory, "results.html");
            if (File.Exists(resultsFile))
            {
                Logger.Debug("Diffoscope run successful.");
                var results = File.ReadAllText(resultsFile);
                return results;
            }
            else
            {
                Logger.Debug("Diffoscope result file was empty.");
                return null;
            }
        }
    }
}