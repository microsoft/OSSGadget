// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Shared;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
    }

    public class StrategyResultMessage
    {
        public StrategyResultMessage()
        {
            this.Text = "";
            this.Filename = "";
            this.Differences = Array.Empty<DiffPiece>();
        }

        public string Text { get; set; }
        public string Filename { get; set; }
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

        
    }
}