using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using SharpCompress;
using SharpCompress.Common;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
    }

    public class StrategyResult
    {
        public StrategyResult()
        {
            Messages = new List<string>();
        }

        [JsonIgnore]
        public Type? Strategy { get; set; }

        public string? StrategyName { get => Strategy?.Name; }
        public string? Summary { get; set; }
        public List<string> Messages { get; set; }
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
        /// This is a failure-resistent version of .CreateFromDirectory or .AddDirectory, both of which fail under various
        /// conditions. This function continues on (emitting a message to the logger) and returns false on any error.
        /// </summary>
        /// <param name="directoryName">Directory to zip</param>
        /// <param name="archiveName">File to write to.</param>
        /// <returns>true iff no errors occur</returns>
        protected static bool CreateZipFromDirectory(string directoryName, string archiveName)
        {
            var result = true;
            // Note that we're not using something like .CreateFromDirectory, or .AddDirectory, since both of these had problems
            // with permissions. Instead, we'll try to add each file separately, and continue on any failures.
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
        public static bool RunCommand(string workingDirectory, string filename, IEnumerable<string> args, out string? stdout, out string? stderr)
        {
            Logger.Debug("RunCommand({0})", filename);

            var startInfo = new ProcessStartInfo()
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = filename,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                stdout = null;
                stderr = null;
                return false;
            }
            var sbStdout = new StringBuilder();
            var sbStderr = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Logger.Debug("OUT: {0}", args.Data);
                    sbStdout.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Logger.Debug("ERR: {0}", args.Data);
                    sbStderr.AppendLine(args.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Apply a timeout 
            var timeout = 1000 * 60 * 15; // 15 minute default timeout
            var envTimeout = Environment.GetEnvironmentVariable("OSS_REPRODUCIBLE_COMMAND_TIMEOUT");
            if (envTimeout != null)
            {
                if (int.TryParse(envTimeout, out var envTimeoutInt))
                {
                    timeout = envTimeoutInt;
                }
            }
            process.WaitForExit(timeout);
            
            stdout = sbStdout.ToString();
            stderr = sbStderr.ToString();

            return process.ExitCode == 0;
        }
    }
}
