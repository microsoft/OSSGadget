// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    public enum DirectoryDifferenceOperation
    {
        Added,
        Removed,
        Equals,
        Modified
    }

    public class DirectoryDifference
    {
        public string Filename { get; set; } = "";
        public DirectoryDifferenceOperation Operation { get; set; }
        public string? ComparisonFile { get; set; }
        public IEnumerable<DiffPiece>? Difference { get; set; }
        //public SideBySideDiffModel? Differences { get; set; }
    }

    public class Helpers
    {
        
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static Helpers()
        {
        }

        internal static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs = true)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        public static IEnumerable<DirectoryDifference> GetDirectoryDifferenceByFilename(string? leftDirectory, string? rightDirectory, PackageURL? packageUrl, string? strategyName)
        {
            var results = new List<DirectoryDifference>();

            Logger.Debug("GetDirectoryDifferenceByFilename({0}, {1})", leftDirectory, rightDirectory);
            if (leftDirectory == null || rightDirectory == null ||
                !Directory.Exists(leftDirectory) || !Directory.Exists(rightDirectory))
            {
                Logger.Warn("Directory does not exist.");
                throw new DirectoryNotFoundException("Directory does not exist.");
            }

            var leftFiles = Directory.EnumerateFiles(leftDirectory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 });
            // https://stackoverflow.com/questions/2070356/find-common-prefix-of-strings
            var commonLeftPrefix = new string(leftFiles.First().Substring(0, leftFiles.Min(s => s.Length)).TakeWhile((c, i) => leftFiles.All(s => s[i] == c)).ToArray());
            leftFiles = leftFiles.Select(s => s[commonLeftPrefix.Length..]);

            var rightFiles = Directory.EnumerateFiles(rightDirectory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 });
            var commonRightPrefix = new string(rightFiles.First().Substring(0, rightFiles.Min(s => s.Length)).TakeWhile((c, i) => rightFiles.All(s => s[i] == c)).ToArray());
            rightFiles = rightFiles.Select(s => s[commonRightPrefix.Length..]);

            results.AddRange(rightFiles.Except(leftFiles).Select(f => new DirectoryDifference { Filename = f, Operation = DirectoryDifferenceOperation.Added }));
            results.AddRange(leftFiles.Except(rightFiles).Select(f => new DirectoryDifference { Filename = f, Operation = DirectoryDifferenceOperation.Removed }));
            results.AddRange(leftFiles.Intersect(rightFiles).Where(f =>
            {
                var leftFile = Path.Join(commonLeftPrefix, f);
                var rightFile = Path.Join(commonRightPrefix, f);

                if (new FileInfo(leftFile).Length != new FileInfo(rightFile).Length)
                {
                    return true;
                }
                if (!File.ReadAllBytes(leftFile).SequenceEqual(File.ReadAllBytes(rightFile)))
                {
                    return true;
                }
                return false;
            }).Select(f => new DirectoryDifference { Filename = f, Operation = DirectoryDifferenceOperation.Modified }));

            var resultsWithFilter = results.Where(dd => !IgnoreFilter.IsIgnored(packageUrl, strategyName!, dd.Filename));
            return resultsWithFilter;
        }

        /// <summary>
        /// Identifies all elements in leftDirectory that either don't exist in rightDirectory or
        /// exist with different content. This function is "smart" in that it is resilient to
        /// changes in directory names, meaning: leftDirectory, file = /foo/bar/quux/baz.txt
        /// rightDirectory, file = /bing/quux/baz.txt These would be correctly classified as the
        /// same file. If there was another file in rightDirectory: rightDirectory, file =
        /// /qwerty/bing/quux/baz.txt Then that one would match better, since it has a longer suffix
        /// in common with the leftDirectory file.
        /// </summary>
        /// <param name="leftDirectory">Typically the existing package</param>
        /// <param name="rightDirectory">Source repo, or built package, etc.</param>
        /// <returns></returns>
        public static IEnumerable<DirectoryDifference> DirectoryDifference(string leftDirectory, string rightDirectory, DiffTechnique diffTechnique)
        {
            var results = new List<DirectoryDifference>();

            // left = built package, right = source repo
            var leftFiles = Directory.EnumerateFiles(leftDirectory, "*", SearchOption.AllDirectories);
            var rightFiles = Directory.EnumerateFiles(rightDirectory, "*", SearchOption.AllDirectories);

            foreach (var leftFile in leftFiles)
            {
                var closestMatches = GetClosestFileMatch(leftFile, rightFiles);
                var closestMatch = closestMatches.FirstOrDefault();
                if (closestMatch == null)
                {
                    results.Add(new DirectoryDifference()
                    {
                        Filename = leftFile[leftDirectory.Length..].Replace("\\", "/"),
                        ComparisonFile = null,
                        Operation = DirectoryDifferenceOperation.Added
                    });
                }
                else
                {
                    var filenameContent = File.ReadAllText(leftFile);
                    var closestMatchContent = File.ReadAllText(closestMatch);
                    if (!string.Equals(filenameContent, closestMatchContent))
                    {
                        if (diffTechnique == DiffTechnique.Normalized)
                        {
                            filenameContent = NormalizeContent(leftFile);
                            closestMatchContent = NormalizeContent(closestMatch);
                        }
                    }

                    var diff = InlineDiffBuilder.Diff(filenameContent, closestMatchContent, ignoreWhiteSpace: true, ignoreCase: false);
                    if (diff.HasDifferences)
                    {
                        results.Add(new DirectoryDifference()
                        {
                            Filename = leftFile[leftDirectory.Length..].Replace("\\", "/"),
                            ComparisonFile = closestMatch[rightDirectory.Length..].Replace("\\", "/"),
                            Operation = DirectoryDifferenceOperation.Modified,
                            Difference = diff.Lines
                        });
                    }
                }
            }
            return results;
        }
        public static bool RunCommand(string workingDirectory, string filename, IEnumerable<string> args, out string? stdout, out string? stderr)
        {
            Logger.Debug("RunCommand({0}, {1})", filename, string.Join(';', args));

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

            var timer = new Stopwatch();
            timer.Start();

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                stdout = null;
                stderr = null;
                return false;
            }
            var sbStdout = new StringBuilder();
            var sbStderr = new StringBuilder();
            var sbStdoutLock = new Object();
            var sbStderrLock = new Object();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Logger.Trace("OUT: {0}", args.Data);
                    lock (sbStdoutLock)
                    {
                        sbStdout.AppendLine(args.Data);
                    }
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Logger.Trace("ERR: {0}", args.Data);
                    lock (sbStderrLock)
                    {
                        sbStderr.AppendLine(args.Data);
                    }
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

            lock (sbStderrLock)
            {
                stderr = sbStderr.ToString();
            }
            lock (sbStdoutLock)
            {
                stdout = sbStdout.ToString();
            }
            
            timer.Stop();
            Logger.Debug("Elapsed time: {0}s", timer.Elapsed.TotalSeconds);
            Logger.Debug("Exit Code: {0}", process.ExitCode);
            
            return process.ExitCode == 0;
        }

        /// <summary>
        /// Delete a directory, with retries in case of failure.
        /// </summary>
        /// <param name="directoryName">Directory to delete</param>
        /// <param name="numTries">Number of attempts to make</param>
        public static void DeleteDirectory(string directoryName, int numTries = 3)
        {
            if (!Directory.Exists(directoryName))
            {
                return;
            }
            int delayMs = 1000;

            // Clean up our temporary directory
            while (numTries > 0)
            {
                try
                {
                    Directory.Delete(directoryName, true);
                    break;
                }
                catch (Exception)
                {
                    Logger.Debug("Error deleting [{0}], sleeping for {1} seconds.", directoryName, delayMs);
                    Thread.Sleep(delayMs);
                    delayMs *= 2;
                    numTries--;
                }
            }
        }

        /// <summary>
        /// Attempts to "normalize" source code content by beautifying it. In some cases, this can
        /// remove trivial differences.
        /// Uses the NPM 'prettier' module within a docker container.
        /// </summary>
        /// <param name="filename">File to normalize</param>
        /// <returns>Normalized content, or the raw file content.</returns>
        public static string NormalizeContent(string filename)
        {
            if (filename.EndsWith(".js") || filename.EndsWith(".ts"))
            {
                Logger.Debug("Normalizing {0}", filename);
                var tempDirectoryName = Guid.NewGuid().ToString();
                if (!Directory.Exists(tempDirectoryName))
                {
                    Directory.CreateDirectory(tempDirectoryName);
                }
                var extension = Path.GetExtension(filename);
                var tempFile = Path.ChangeExtension(Path.Join(tempDirectoryName, "temp"), extension);
                var bytes = File.ReadAllBytes(filename);
                File.WriteAllBytes(tempFile, bytes);

                var runResult = Helpers.RunCommand(tempDirectoryName, "docker", new[] {
                                            "run",
                                            "--rm",
                                            "--memory=1g",
                                            "--cpus=1.0",
                                            "--volume", $"{Path.GetFullPath(tempDirectoryName)}:/repo",
                                            "--workdir=/repo",
                                            "tmknom/prettier",
                                            Path.ChangeExtension("/repo/temp", extension)
                                       }, out var stdout, out var stderr);

                Helpers.DeleteDirectory(tempDirectoryName);
                if (stdout != null)
                {
                    return stdout;
                }
                else
                {
                    return File.ReadAllText(filename);
                }
            }
            else
            {
                return File.ReadAllText(filename);
            }
        }

        /// <summary>
        /// Identifes the closest filename match to the target filename.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="filenames"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetClosestFileMatch(string target, IEnumerable<string> filenames)
        {
            target = target.Replace("\\", "/").Trim().Trim(' ', '/');
            filenames = filenames.Select(f => f.Replace("\\", "/").Trim().TrimEnd('/'));

            var candidate = "";

            var bestCandidates = new HashSet<string>();
            var bestCandidateScore = 0;
            
            var targetNumDirs = target.Count(ch => ch == '/') + 1;

            foreach (var part in target.Split('/').Reverse())
            {
                candidate = Path.Join(part, candidate).Replace("\\", "/");
                foreach (var filename in filenames)
                {
                    if (filename.EndsWith(candidate, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var candidateScore = candidate.Count(ch => ch == '/');
                        if (candidateScore > bestCandidateScore)
                        {
                            bestCandidateScore = candidateScore;
                            bestCandidates.Clear();
                            bestCandidates.Add(filename);
                        }
                        else if (candidateScore == bestCandidateScore)
                        {
                            bestCandidates.Add(filename);
                        }
                    }
                }
            }
            var resultList = bestCandidates.ToList();
            resultList.Sort((a, b) => a.Length.CompareTo(b.Length));
            resultList.Reverse();
            return resultList;
        }

        internal static string? GetFirstNonSingularDirectory(string? directory)
        {
            if (directory == null || !Directory.Exists(directory))
            {
                return null;
            }
            var entries = Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly);
            if (entries.Count() == 1)
            {
                var firstEntry = entries.First();
                if (Directory.Exists(firstEntry))
                {
                    return GetFirstNonSingularDirectory(firstEntry);
                }
                else
                {
                    return directory;
                }
            }
            else
            {
                return directory;
            }
        }

        internal static void AddDifferencesToStrategyResult(StrategyResult strategyResult, IEnumerable<DirectoryDifference> directoryDifferences, bool reverseDirection = false)
        {
            if (!directoryDifferences.Any())
            {
                strategyResult.Summary = "Successfully reproduced package.";
                strategyResult.IsSuccess = true;
                Logger.Debug("Strategy succeeded. The results match the package contents.");
            }
            else
            {
                strategyResult.Summary = "Strategy failed. The results do not match the package contents.";
                strategyResult.IsSuccess = false;

                foreach (var dirDiff in directoryDifferences)
                {
                    var message = new StrategyResultMessage()
                    {
                        Filename = reverseDirection ? dirDiff.ComparisonFile : dirDiff.Filename,
                        CompareFilename = reverseDirection ? dirDiff.Filename : dirDiff.ComparisonFile,
                        Differences = dirDiff.Difference,
                    };
                    switch (dirDiff.Operation)
                    {
                        case DirectoryDifferenceOperation.Added:
                            message.Text = "File added"; break;
                        case DirectoryDifferenceOperation.Modified:
                            message.Text = "File modified"; break;
                        case DirectoryDifferenceOperation.Removed:
                            message.Text = "File removed"; break;
                        default:
                            break;
                    }
                    strategyResult.Messages.Add(message);
                }
            }
        }
        //public string ConvertStrategyResult(StrategyResult strategyResult)
        //{
        //}

        /*
        internal string ConvertSideBySideDiffModelToText(SideBySideDiffModel diff)
        {
            var leftSide = new Dictionary<int, KeyValuePair<ChangeType, string>>();
            var rightSide = new Dictionary<int, KeyValuePair<ChangeType, string>>();

            var oldText = diff.OldText;
            foreach (var d in diff.OldText.Lines)
            {
                if (d.Position != null)
                {
                    leftSide[(int)d.Position] = KeyValuePair.Create<ChangeType, string>(d.Type, d.Text);
                }
            }
            foreach (var d in diff.NewText.Lines)
            {
                if (d.Position != null)
                {
                    rightSide[(int)d.Position] = KeyValuePair.Create<ChangeType, string>(d.Type, d.Text);
                }
            }

            foreach 
        }
        */
    }
}