// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.OSSGadget.Shared.Cli.IntegrationTests.Helpers;

using Microsoft.CST.OpenSource;
using Microsoft.CST.OpenSource.Helpers;
using Microsoft.CST.OpenSource.PackageManagers;
using PackageUrl;

public class PackageDownloaderTheoryData :TheoryData<string,string,int>
{
    public PackageDownloaderTheoryData()
    {
        Add("pkg:cpan/Data-Rand@0.0.4", "MANIFEST", 1);
    }

}
public class PackageDownloaderTests
{
    [IntegrationTestTheory]
    [ClassData(typeof(PackageDownloaderTheoryData))]
    public async Task When_package_downloaded_then_specified_file_found(string purl, string targetFilename, int expectedDirectoryCount)
    {
        string tempDirectoryName = CreateTemporaryDirectory();
        string errorString = string.Empty;

        try
        {
            PackageURL? packageUrl = new(purl);
            PackageDownloader packageDownloader = packageDownloader = new PackageDownloader(packageUrl, new ProjectManagerFactory(), tempDirectoryName, false);
            List<string> directories = await packageDownloader.DownloadPackageLocalCopy(packageUrl, false, true);
            if(directories.Count == 0)
            {
                errorString = "Package download resulted in 0 directories. ";
            }

            bool targetFileWasDownloaded = Directory.EnumerateFiles(tempDirectoryName, targetFilename, SearchOption.AllDirectories).Any();
            if (!targetFileWasDownloaded)
            {
                errorString += "Target file was not downloaded. ";
            }

            int topLevelDirectoryCount = Directory.GetDirectories(tempDirectoryName).Length;
            if (expectedDirectoryCount != topLevelDirectoryCount)
            {
                errorString += $"Directory count {topLevelDirectoryCount} does not match expected {expectedDirectoryCount}";
            }

            // one delete is enough, since its only a single cached copy
            DeleteTempDirs(packageDownloader, tempDirectoryName);
        }
        catch (Exception ex)
        {
            throw new Exception("Error", ex);
        }

        if(!string.IsNullOrEmpty(errorString))
        {
            throw new Exception(errorString);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string? tempDirectoryName = null;
        while (tempDirectoryName == null || Directory.Exists(tempDirectoryName) || File.Exists(tempDirectoryName))
        {
            tempDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }
        Directory.CreateDirectory(tempDirectoryName);
        return tempDirectoryName;
    }

    private static void DeleteTempDirs(PackageDownloader? packageDownloader, string tempDirectoryName)
    {
        try
        {
            packageDownloader?.ClearPackageLocalCopyIfNoCaching();
        }
        catch (Exception)
        {
            foreach (string? filename in Directory.EnumerateFileSystemEntries(tempDirectoryName, "*", SearchOption.AllDirectories))
            {
                FileInfo _ = new(filename)
                {
                    Attributes = FileAttributes.Normal
                };
            }
            packageDownloader?.ClearPackageLocalCopyIfNoCaching();
        }
        finally
        {
            FileSystemHelper.RetryDeleteDirectory(tempDirectoryName);
        }
    }
}
