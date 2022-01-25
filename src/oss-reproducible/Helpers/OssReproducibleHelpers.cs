// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Helpers;
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

    public class OssReproducibleHelpers
    {
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static OssReproducibleHelpers()
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
            List<DirectoryDifference>? results = new List<DirectoryDifference>();

            // left = built package, right = source repo
            IEnumerable<string>? leftFiles = Directory.EnumerateFiles(leftDirectory, "*", SearchOption.AllDirectories);
            IEnumerable<string>? rightFiles = Directory.EnumerateFiles(rightDirectory, "*", SearchOption.AllDirectories);

            foreach (string? leftFile in leftFiles)
            {
                IEnumerable<string>? closestMatches = GetClosestFileMatch(leftFile, rightFiles);
                string? closestMatch = closestMatches.FirstOrDefault();
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
                    string? filenameContent = File.ReadAllText(leftFile);
                    string? closestMatchContent = File.ReadAllText(closestMatch);
                    if (!string.Equals(filenameContent, closestMatchContent))
                    {
                        if (diffTechnique == DiffTechnique.Normalized)
                        {
                            filenameContent = NormalizeContent(leftFile);
                            closestMatchContent = NormalizeContent(closestMatch);
                        }
                    }

                    DiffPaneModel? diff = InlineDiffBuilder.Diff(filenameContent, closestMatchContent, ignoreWhiteSpace: true, ignoreCase: false);
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

            ProcessStartInfo? startInfo = new ProcessStartInfo()
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = filename,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory
            };
            foreach (string? arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            Stopwatch? timer = new Stopwatch();
            timer.Start();

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                stdout = null;
                stderr = null;
                return false;
            }
            StringBuilder? sbStdout = new StringBuilder();
            StringBuilder? sbStderr = new StringBuilder();
            object? sbStdoutLock = new Object();
            object? sbStderrLock = new Object();

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
            int timeout = 1000 * 60 * 15; // 15 minute default timeout
            string? envTimeout = Environment.GetEnvironmentVariable("OSS_REPRODUCIBLE_COMMAND_TIMEOUT");
            if (envTimeout != null)
            {
                if (int.TryParse(envTimeout, out int envTimeoutInt))
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
        /// Attempts to "normalize" source code content by beautifying it. In some cases, this can
        /// remove trivial differences. Uses the NPM 'prettier' module within a docker container.
        /// </summary>
        /// <param name="filename">File to normalize</param>
        /// <returns>Normalized content, or the raw file content.</returns>
        public static string NormalizeContent(string filename)
        {
            if (filename.EndsWith(".js") || filename.EndsWith(".ts"))
            {
                Logger.Debug("Normalizing {0}", filename);
                string? tempDirectoryName = Guid.NewGuid().ToString();
                if (!Directory.Exists(tempDirectoryName))
                {
                    Directory.CreateDirectory(tempDirectoryName);
                }
                string? extension = Path.GetExtension(filename);
                string? tempFile = Path.ChangeExtension(Path.Join(tempDirectoryName, "temp"), extension);
                byte[]? bytes = File.ReadAllBytes(filename);
                File.WriteAllBytes(tempFile, bytes);

                bool runResult = RunCommand(tempDirectoryName, "docker", new[] {
                                            "run",
                                            "--rm",
                                            "--memory=1g",
                                            "--cpus=1.0",
                                            "--volume", $"{Path.GetFullPath(tempDirectoryName)}:/repo",
                                            "--workdir=/repo",
                                            "tmknom/prettier",
                                            Path.ChangeExtension("/repo/temp", extension)
                                       }, out string? stdout, out string? stderr);

                FileSystemHelper.RetryDeleteDirectory(tempDirectoryName);
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

            string? candidate = "";

            HashSet<string>? bestCandidates = new HashSet<string>();
            int bestCandidateScore = 0;

            int targetNumDirs = target.Count(ch => ch == '/') + 1;

            foreach (string? part in target.Split('/').Reverse())
            {
                candidate = Path.Join(part, candidate).Replace("\\", "/");
                foreach (string? filename in filenames)
                {
                    if (filename.EndsWith(candidate, StringComparison.InvariantCultureIgnoreCase))
                    {
                        int candidateScore = candidate.Count(ch => ch == '/');
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
            List<string>? resultList = bestCandidates.ToList();
            resultList.Sort((a, b) => 1 - a.Length.CompareTo(b.Length));
            return resultList;
        }

        internal static string? GetFirstNonSingularDirectory(string? directory)
        {
            if (directory == null || !Directory.Exists(directory))
            {
                return null;
            }
            IEnumerable<string>? entries = Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly);
            if (entries.Count() == 1)
            {
                string? firstEntry = entries.First();
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
    }
}