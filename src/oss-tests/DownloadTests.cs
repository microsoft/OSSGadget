// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Contracts;
using Microsoft.CST.OpenSource.Helpers;
using PackageManagers;
using PackageUrl;
using System.Collections.Generic;

public class DownloadTests
{
    [Theory]
    [InlineData("pkg:cargo/rand@0.7.3", "CARGO.toml", 1)]
    [InlineData("pkg:cargo/rand", "CARGO.toml", 1)]
    public async Task Cargo_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:cocoapods/RandomKit", "RandomKit.podspec", 1)]
    public async Task Cocoapods_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:composer/ircmaxell/random-lib", "composer.json", 1)]
    public async Task Composer_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:cpan/Data-Rand", "MANIFEST", 1)]
    public async Task CPAN_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:cran/Archive/ACNE", "DESCRIPTION", 1)]
    public async Task CRAN_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:gem/zlib@0.1.0", "zlib.gemspec", 1)]
    public async Task Gem_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:github/ruby/zlib", "zlib.gemspec", 1)]
    public async Task GitHub_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:golang/sigs.k8s.io/yaml", "yaml.go", 1)]
    [InlineData("pkg:golang/github.com/Azure/go-autorest@v0.11.28#autorest", "go.mod", 1)]
    public async Task Golang_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:golang/sigs.k8s.io/yaml", "does-not-exist", 37)]
    public async Task Golang_Download_Version_Fails(string purl, string targetFilename, int expectedDirectoryCount)
    {
        var action = async () => await TestDownload(purl, targetFilename, expectedDirectoryCount);

        await action.Should().ThrowAsync<DownloadTestException>("Expected a test failure but one did not occur.");
    }

    [Theory]
    [InlineData("pkg:hackage/a50", "a50.cabal", 1)]
    public async Task Hackage_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:invalid/invalid", "", 1)]
    public async Task Invalid_Package_Test_Download(string purl, string targetFilename, int expectedDirectoryCount)
    {
        var action = async () => await TestDownload(purl, targetFilename, expectedDirectoryCount);

        await action.Should().ThrowAsync<DownloadTestException>("Expected a ArgumentException but no exception was thrown.");
    }

    [Theory]
    [InlineData("ckg:invalid@invalid@invalid", "", 0)]
    public async Task Invalid_Purl_Test_Download(string purl, string targetFilename, int expectedDirectoryCount)
    {
        var action = async () => await TestDownload(purl, targetFilename, expectedDirectoryCount);

        await action.Should().ThrowAsync<DownloadTestException>(
            "Expected a FormatException but no exception was thrown.");
    }

    [Theory]
    [InlineData("pkg:maven/org%2Fapache%2Fxmlgraphics/batik-anim@1.9", "MANIFEST.MF", 3)]
    [InlineData("pkg:maven/ant/ant-junit@1.6.5", "MANIFEST.MF", 1)] // this project only has a jar file
    public async Task Maven_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:npm/left-pad@1.3.0", "package.json", 1)]
    [InlineData("pkg:npm/md5", "package.json", 1)]
    public async Task NPM_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:npm/%40angular%2Fanimation@4.0.0-beta.8", "package.json", 1)]
    public async Task NPM_Download_ScopedVersion_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:nuget/RandomType@2.0.0", "RandomType.nuspec", 1)]
    [InlineData("pkg:nuget/d3.TypeScript.DefinitelyTyped", "d3.TypeScript.DefinitelyTyped.nuspec", 1)]
    [InlineData("pkg:nuget/boxer@0.1.0-preview1", "boxer.nuspec", 1)]
    public async Task NuGet_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }
    
    [Theory]
    [InlineData("pkg:npm/moment@*", "package.json")]
    [InlineData("pkg:nuget/RandomType@*", "RandomType.nuspec")]
    [InlineData("pkg:nuget/Newtonsoft.Json@*", "newtonsoft.json.nuspec")]
    public async Task Wildcard_Download_Version_Succeeds(string packageUrl, string targetFilename)
    {
        PackageURL purl = new(packageUrl);
        IBaseProjectManager? manager = new ProjectManagerFactory().CreateProjectManager(purl);
        IEnumerable<string> versions = await manager?.EnumerateVersionsAsync(purl) ?? throw new InvalidOperationException();
        await TestDownload(packageUrl, targetFilename, versions.Count());
    }

    [Theory]
    [InlineData(null, null, 1)]
    public async Task Null_Test_Download(string purl, string targetFilename, int expectedDirectoryCount)
    {
        var action = async () => await TestDownload(purl, targetFilename, expectedDirectoryCount);

        await action.Should().ThrowAsync<DownloadTestException>(
            "Expected a FormatException but no exception was thrown.");
    }

    [Theory]
    [InlineData("pkg:pypi/bz2file@0.98", "PKG-INFO", 1)]
    public async Task PyPI_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    [Theory]
    [InlineData("pkg:ubuntu/zerofree", "zerofree.c", 4)]
    public async Task Ubuntu_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        // The Ubuntu endpoints fail occasionally, but often just come back a few seconds later,
        // so try it a few times
        int numAttempts = 3;
        bool isSuccess = false;
        while (numAttempts > 0 && !isSuccess)
        {
            try
            {
                TestDownload(purl, targetFilename, expectedDirectoryCount).Wait();
                isSuccess = true; // Successful!
            }
            catch (Exception)
            {
                Thread.Sleep(10000);
                numAttempts--;
            }
        }
        Assert.True(isSuccess);
    }

    [Theory]
    [InlineData("pkg:ubuntu/nonexistent12345", "nonexistent.123", 1)]
    public async Task Ubuntu_Download_Version_NonExistent_Fails(string purl, string targetFilename, int expectedDirectoryCount)
    {
        var action = async () => await TestDownload(purl, targetFilename, expectedDirectoryCount);

        await action.Should().ThrowAsync<DownloadTestException>(
            "Expected an DownloadTestException due to a non-existent package.");
    }

    [Theory]
    // The latest powershell has a second directory for code signing information
    [InlineData("pkg:vsm/ms-vscode/PowerShell", "extension.vsixmanifest", 2)]
    [InlineData("pkg:vsm/ms-vscode/PowerShell@2020.6.0", "extension.vsixmanifest", 1)]
    [InlineData("pkg:vsm/liviuschera/noctis@10.39.1", "extension.vsixmanifest", 1)]
    public async Task VSM_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
    {
        await TestDownload(purl, targetFilename, expectedDirectoryCount);
    }

    /// <summary>
    /// delete the package download
    /// </summary>
    /// <param name="packageDownloader"></param>
    /// <param name="tempDirectoryName"></param>
    private void deleteTempDirs(PackageDownloader? packageDownloader, string tempDirectoryName)
    {
        try
        {
            packageDownloader?.ClearPackageLocalCopyIfNoCaching();
        }
        catch (Exception)
        {
            foreach (string? filename in Directory.EnumerateFileSystemEntries(tempDirectoryName, "*", SearchOption.AllDirectories))
            {
                FileInfo? fileInfo = new(filename)
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

    /// <summary>
    /// Download the package
    /// </summary>
    /// <param name="packageUrl"></param>
    /// <param name="tempDirectoryName"></param>
    /// <returns></returns>
    private PackageDownloader? DownloadPackage(PackageURL packageUrl, string tempDirectoryName, bool doCache = false)
    {
        int numAttempts = 3;
        int numSecondsWait = 10;
        PackageDownloader? packageDownloader = null;
        while (numAttempts-- > 0)
        {
            try
            {
                packageDownloader = new PackageDownloader(packageUrl, new ProjectManagerFactory(), tempDirectoryName, doCache);
                packageDownloader.DownloadPackageLocalCopy(packageUrl, false, true).Wait();
                break;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception)
            {
                Thread.Sleep(numSecondsWait * 1000);
            }
        }
        return packageDownloader;
    }

    private async Task TestDownload(string purl, string targetFilename, int expectedDirectoryCount)
    {
        string? tempDirectoryName = null;
        while (tempDirectoryName == null || Directory.Exists(tempDirectoryName) || File.Exists(tempDirectoryName))
        {
            tempDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        Directory.CreateDirectory(tempDirectoryName);
        string? errorString = null;

        try
        {
            PackageURL? packageUrl = new(purl);
            PackageDownloader? packageDownloader = DownloadPackage(packageUrl, tempDirectoryName);

            bool targetFileWasDownloaded = Directory.EnumerateFiles(tempDirectoryName, targetFilename, SearchOption.AllDirectories).Any();
            if (!targetFileWasDownloaded)
            {
                errorString = "Target file was not downloaded.";
            }

            int topLevelDirectoryCount = Directory.GetDirectories(tempDirectoryName).Length;
            if (expectedDirectoryCount != topLevelDirectoryCount)
            {
                errorString = string.Format("Directory count {0} does not match expected {1}", topLevelDirectoryCount, expectedDirectoryCount);
            }

            // Download again (with caching) - TODO, move this to a separate test.
            packageDownloader = DownloadPackage(packageUrl, tempDirectoryName, true);

            // Re-calculate the top level directories, since in might have changed (it shouldn't).
            topLevelDirectoryCount = Directory.GetDirectories(tempDirectoryName).Length;
            if (expectedDirectoryCount != topLevelDirectoryCount)
            {
                errorString = string.Format("Directory count {0} does not match expected {1}", topLevelDirectoryCount, expectedDirectoryCount);
            }

            // one delete is enough, since its only a single cached copy
            deleteTempDirs(packageDownloader, tempDirectoryName);
        }
        catch (Exception ex)
        {
            throw new DownloadTestException("Error", ex);
        }

        if (errorString != null)
        {
            throw new DownloadTestException(errorString);
        }
    }

    internal class DownloadTestException : Exception
    {
        public DownloadTestException()
        {
        }

        public DownloadTestException(string? message) : base(message)
        {
        }

        public DownloadTestException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}