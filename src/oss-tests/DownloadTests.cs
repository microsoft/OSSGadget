// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class DownloadTests
    {
        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            CommonInitialization.Initialize();
        }

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3", "CARGO.toml", 1)]
        [DataRow("pkg:cargo/rand", "CARGO.toml", 1)]
        public async Task Cargo_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:cocoapods/RandomKit", "RandomKit.podspec", 1)]
        public async Task Cocoapods_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:composer/ircmaxell/random-lib", "composer.json", 1)]
        public async Task Composer_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:cpan/Data-Rand", "MANIFEST", 1)]
        public async Task CPAN_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:cran/Archive/ACNE", "DESCRIPTION", 1)]
        public async Task CRAN_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:gem/zlib@0.1.0", "zlib.gemspec", 1)]
        public async Task Gem_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:github/ruby/zlib", "zlib.gemspec", 1)]
        public async Task GitHub_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:hackage/a50", "a50.cabal", 1)]
        public async Task Hackage_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:invalid/invalid", "", 1)]
        public async Task Invalid_Package_Test_Download(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await Assert.ThrowsExceptionAsync<InternalTestFailureException>(async () =>
            {
                await TestDownload(purl, targetFilename, expectedDirectoryCount);
            }, "Expected a ArgumentException but no exception was thrown.");
        }

        [DataTestMethod]
        [DataRow("ckg:invalid@invalid@invalid", "", 0)]
        public async Task Invalid_Purl_Test_Download(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await Assert.ThrowsExceptionAsync<InternalTestFailureException>(async () =>
            {
                await TestDownload(purl, targetFilename, expectedDirectoryCount);
            }, "Expected a FormatException but no exception was thrown.");
        }

        [DataTestMethod]
        [DataRow("pkg:maven/org%2Fapache%2Fxmlgraphics/batik-anim@1.9", "MANIFEST.MF", 3)]
        public async Task Maven_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/left-pad@1.3.0", "package.json", 1)]
        [DataRow("pkg:npm/md5", "package.json", 1)]
        public async Task NPM_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/%40angular%2Fanimation@4.0.0-beta.8", "package.json", 1)]
        public async Task NPM_Download_ScopedVersion_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/RandomType@2.0.0", "RandomType.nuspec", 1)]
        [DataRow("pkg:nuget/d3.TypeScript.DefinitelyTyped", "d3.TypeScript.DefinitelyTyped.nuspec", 1)]
        public async Task NuGet_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow(null, null, 1)]
        public async Task Null_Test_Download(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await Assert.ThrowsExceptionAsync<InternalTestFailureException>(async () =>
            {
                await TestDownload(purl, targetFilename, expectedDirectoryCount);
            }, "Expected a FormatException but no exception was thrown.");
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/bz2file@0.98", "PKG-INFO", 1)]
        public async Task PyPI_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:ubuntu/zerofree", "zerofree.c", 4)]
        public async Task Ubuntu_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        [DataTestMethod]
        [DataRow("pkg:ubuntu/nonexistent12345", "nonexistent.123", 1)]
        public async Task Ubuntu_Download_Version_NonExistent_Fails(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await Assert.ThrowsExceptionAsync<InternalTestFailureException>(async () =>
           {
               await TestDownload(purl, targetFilename, expectedDirectoryCount);
           }, "Expected an InternalTestFailureException due to a non-existent package.");
        }

        [DataTestMethod]
        [DataRow("pkg:vsm/ms-vscode/PowerShell", "extension.vsixmanifest", 1)]
        public async Task VSM_Download_Version_Succeeds(string purl, string targetFilename, int expectedDirectoryCount)
        {
            await TestDownload(purl, targetFilename, expectedDirectoryCount);
        }

        /// <summary>
        ///     delete the package download
        /// </summary>
        /// <param name="packageDownloader"> </param>
        /// <param name="tempDirectoryName"> </param>
        private void deleteTempDirs(PackageDownloader? packageDownloader, string tempDirectoryName)
        {
            try
            {
                packageDownloader?.ClearPackageLocalCopyIfNoCaching();
                Directory.Delete(tempDirectoryName, true);
            }
            catch (Exception)
            {
                foreach (var filename in Directory.EnumerateFileSystemEntries(tempDirectoryName, "*", SearchOption.AllDirectories))
                {
                    var fileInfo = new FileInfo(filename)
                    {
                        Attributes = FileAttributes.Normal
                    };
                }
                packageDownloader?.ClearPackageLocalCopyIfNoCaching();
                Directory.Delete(tempDirectoryName, true);
            }
        }

        /// <summary>
        ///     Download the package
        /// </summary>
        /// <param name="packageUrl"> </param>
        /// <param name="tempDirectoryName"> </param>
        /// <returns> </returns>
        private PackageDownloader? DownloadPackage(PackageURL packageUrl, string tempDirectoryName, bool doCache = false)
        {
            int numAttempts = 3;
            int numSecondsWait = 10;
            PackageDownloader? packageDownloader = null;
            while (numAttempts-- > 0)
            {
                try
                {
                    packageDownloader = new PackageDownloader(packageUrl, tempDirectoryName, doCache);
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
                var packageUrl = new PackageURL(purl);
                var packageDownloader = DownloadPackage(packageUrl, tempDirectoryName);

                var targetFileWasDownloaded = Directory.EnumerateFiles(tempDirectoryName, targetFilename, SearchOption.AllDirectories).Any();
                if (!targetFileWasDownloaded)
                {
                    errorString = "Target file was not downloaded.";
                }

                var topLevelDirectoryCount = Directory.GetDirectories(tempDirectoryName).Length;
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
            catch(Exception ex)
            {
                throw new InternalTestFailureException("Error", ex);
            }

            if (errorString != null)
            {
                throw new InternalTestFailureException(errorString);
            }
        }
    }
}