// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class DownloadTests
    {

        [DataTestMethod]
        [DataRow("pkg:cargo/rand@0.7.3", "CARGO.toml", 1)]
        [DataRow("pkg:cargo/rand", "CARGO.toml", 1)]
        public async Task Cargo_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:cocoapods/RandomKit", "RandomKit.podspec", 1)]
        public async Task Cocoapods_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:composer/ircmaxell/random-lib", "composer.json", 1)]
        public async Task Composer_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:cpan/Data-Rand", "MANIFEST", 1)]
        public async Task CPAN_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:cran/Archive/ACNE", "DESCRIPTION", 1)]
        public async Task CRAN_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:gem/zlib@0.1.0", "zlib.gemspec", 1)]
        public async Task Gem_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:github/ruby/zlib", "zlib.gemspec", 1)]
        public async Task GitHub_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:hackage/a50", "a50.cabal", 1)]
        public async Task Hackage_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:maven/org%2Fapache%2Fxmlgraphics/batik-anim@1.9", "MANIFEST.MF", 1)]
        public async Task Maven_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/left-pad@1.3.0", "package.json", 1)]
        [DataRow("pkg:npm/md5", "package.json", 1)]
        public async Task NPM_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/RandomType@2.0.0", "RandomType.nuspec", 1)]
        public async Task NuGet_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/bz2file@0.98", "PKG-INFO", 1)]
        public async Task PyPI_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }

        [DataTestMethod]
        [DataRow("pkg:ubuntu/zerofree", "zerofree.c", 4)]
        public async Task Ubuntu_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, 4);
        }

        [DataTestMethod]
        [DataRow("pkg:vsm/ms-vscode/PowerShell", "extension.vsixmanifest", 1)]
        public async Task VSM_Download_Version_Succeeds(string purl, string targetFilename, int expectedCount)
        {
            await TestDownload(purl, targetFilename, expectedCount);
        }


        [DataTestMethod]
        [DataRow(null, null, 1)]
        public async Task Null_Test_Download(string purl, string targetFilename, int expectedCount)
        {
            try
            {
                await TestDownload(purl, targetFilename, expectedCount);
            }
            catch(FormatException)
            {
                return;
            }
            Assert.Fail("The right exception did not fire");
        }

        private async Task TestDownload(string purl, string targetFilename, int expectedCount)
        {
            string tempDirectoryName = default;
            while (tempDirectoryName == default || File.Exists(tempDirectoryName))
            {
                tempDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }

            var packageUrl = new PackageURL(purl);
            Assert.IsNotNull(packageUrl);

            Directory.CreateDirectory(tempDirectoryName);
            var downloadTool = new DownloadTool(); // do common initialization
            PackageDownloader packageDownloader1 = await DownloadPackage(packageUrl, tempDirectoryName);
            string dir = Directory.GetCurrentDirectory();
            bool fileMatchPresent = Directory.EnumerateFiles(tempDirectoryName, targetFilename, SearchOption.AllDirectories).Any();
            Assert.IsTrue(fileMatchPresent);

            Assert.AreEqual(expectedCount, Directory.GetDirectories(tempDirectoryName).Count());

            // do that again with caching, this should not do anything since the cache already has the package
            await DownloadPackage(packageUrl, tempDirectoryName, true);

            Assert.AreEqual(expectedCount, Directory.GetDirectories(tempDirectoryName).Count());

            // one delete is enough, since its only a single cached copy
            deleteTempDirs(packageDownloader1, tempDirectoryName);
        }

        /// <summary>
        /// Download the package
        /// </summary>
        /// <param name="packageUrl"></param>
        /// <param name="tempDirectoryName"></param>
        /// <returns></returns>
        private async Task<PackageDownloader> DownloadPackage(PackageURL packageUrl, string tempDirectoryName, bool doCache = false)
        {
            int numAttempts = 3;
            int numSecondsWait = 10;
            PackageDownloader packageDownloader = null;
            while (numAttempts-- > 0)
            {
                try
                {
                    packageDownloader = new PackageDownloader(packageUrl, tempDirectoryName, doCache);
                    packageDownloader.DownloadPackageLocalCopy(packageUrl, false, true).Wait();
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(numSecondsWait * 1000);
                }
            }
            return packageDownloader;
        }

        /// <summary>
        /// delete the package download
        /// </summary>
        /// <param name="packageDownloader"></param>
        /// <param name="tempDirectoryName"></param>
        void deleteTempDirs(PackageDownloader packageDownloader, string tempDirectoryName)
        {
            try
            {
                packageDownloader.ClearPackageLocalCopy();
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
                packageDownloader.ClearPackageLocalCopy();
                Directory.Delete(tempDirectoryName, true);
            }
        }
    }
}
