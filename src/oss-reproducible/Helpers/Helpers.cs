using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
    }

    class Helpers
    {
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static Helpers()
        {

        }
        internal static IEnumerable<KeyValuePair<string, string>> GenerateDirectoryHashes(string directory)
        {
            var results = new List<KeyValuePair<string, string>>();
            var hashFunction = SHA256.Create();

            foreach (var filename in Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
            {
                try
                {
                    var hashValue = hashFunction.ComputeHash(File.ReadAllBytes(filename));
                    var hashString = Convert.ToHexString(hashValue);
                    results.Add(new KeyValuePair<string, string>(filename, hashString));
                }
                catch (Exception)
                {
                    //Logger.Debug("Unable to compute hash for {0}: {1}", filename, ex.Message);
                }
            }

            return results;
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


        public static IEnumerable<DirectoryDifference> GetDirectoryDifferenceByContent(PackageURL packageUrl, string? leftDirectory, string? rightDirectory, string strategyName)
        {
            var results = new List<DirectoryDifference>();

            Logger.Debug("GetDirectoryDifferenceByContent({0}, {1})", leftDirectory, rightDirectory);
            if (leftDirectory == null || rightDirectory == null ||
                !Directory.Exists(leftDirectory) || !Directory.Exists(rightDirectory))
            {
                Logger.Warn("Directory does not exist.");
                throw new DirectoryNotFoundException("Directory does not exist.");
            }

            var leftContent = GenerateDirectoryHashes2(packageUrl, strategyName, leftDirectory);
            var rightContent = GenerateDirectoryHashes2(packageUrl, strategyName, rightDirectory);
            results.AddRange(rightContent.Keys.Except(leftContent.Keys).Select(f => new DirectoryDifference { Filename = string.Join(',', rightContent[f]), Operation = DirectoryDifferenceOperation.Added }));
            results.AddRange(leftContent.Keys.Except(rightContent.Keys).Select(f => new DirectoryDifference { Filename = string.Join(',', leftContent[f]), Operation = DirectoryDifferenceOperation.Removed }));

            return results;
        }

        /*
        internal static IEnumerable<KeyValuePair<string, string>> GenerateDirectoryHashes(PackageURL packageUrl, string directory, bool useFilters = true)
        {
            var hashFunction = SHA256.Create();
            var directoryFiles = Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 });
            var filteredFiles = directoryFiles.Where(f => !IgnoreFilter.IsIgnored(packageUrl, this.GetType().Name, f));
            foreach (var file in filteredFiles)
            {
                var hashValue = hashFunction.ComputeHash(File.ReadAllBytes(file));
                var hashString = Convert.ToHexString(hashValue);
                yield return KeyValuePair.Create<string, string>(file, hashString);
            }
        }
        */

        internal static Dictionary<string, HashSet<string>> GenerateDirectoryHashes2(PackageURL packageUrl, string strategyName, string directory)
        {
            var results = new Dictionary<string, HashSet<string>>();
            var hashFunction = SHA256.Create();
            var directoryFiles = Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 });
            var filteredFiles = directoryFiles.Where(f => !IgnoreFilter.IsIgnored(packageUrl, strategyName, f));
            foreach (var filename in filteredFiles)
            {

                var hashValue = hashFunction.ComputeHash(File.ReadAllBytes(filename));
                var hashString = Convert.ToHexString(hashValue);
                if (results.ContainsKey(hashString))
                {
                    results[hashString].Add(filename);
                }
                else
                {
                    results[hashString] = new HashSet<string>() { filename };
                }
            }
            return results;
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

    }
}
