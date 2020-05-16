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
        [DataRow("pkg:cargo/rand@0.7.3", "CARGO.toml")]
        [DataRow("pkg:cargo/rand", "CARGO.toml")]
        public async Task Cargo_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:cocoapods/RandomKit", "RandomKit.podspec")]
        public async Task Cocoapods_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:composer/ircmaxell/random-lib", "composer.json")]
        public async Task Composer_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:cpan/Data-Rand", "MANIFEST")]
        public async Task CPAN_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:cran/Archive/ACNE", "DESCRIPTION")]
        public async Task CRAN_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:gem/zlib@0.1.0", "zlib.gemspec")]
        public async Task Gem_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:github/ruby/zlib", "zlib.gemspec")]
        public async Task GitHub_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:hackage/a50", "a50.cabal")]
        public async Task Hackage_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:maven/org%2Fapache%2Fxmlgraphics/batik-anim@1.9", "MANIFEST.MF")]
        public async Task Maven_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/left-pad@1.3.0", "package.json")]
        [DataRow("pkg:npm/md5", "package.json")]
        public async Task NPM_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/RandomType@2.0.0", "RandomType.nuspec")]
        public async Task NuGet_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);
        }

        [DataTestMethod]
        [DataRow("pkg:pypi/bz2file@0.98", "PKG-INFO")]
        public async Task PyPI_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);            
        }

        [DataTestMethod]
        [DataRow("pkg:ubuntu/zerofree", "zerofree.c")]
        public async Task Ubuntu_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 4);            
        }

        [DataTestMethod]
        [DataRow("pkg:vsm/ms-vscode/PowerShell", "extension.vsixmanifest")]
        public async Task VSM_Download_Version_Succeeds(string purl, string targetFilename)
        {
            await TestDownload(purl, targetFilename, 1);            
        }

        [DataTestMethod]
        [DataRow(null, null)]
        public async Task Null_Test_Download(string purl, string targetFilename)
        {
            try
            {
                await TestDownload(purl, targetFilename, 1);
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
                tempDirectoryName = Path.GetRandomFileName();
            }

            var packageUrl = new PackageURL(purl);
            Assert.IsNotNull(packageUrl);

            Directory.CreateDirectory(tempDirectoryName);
            var downloadTool = new DownloadTool(); // do common initialization
            PackageDownloader packageDownloader1 = await DownloadPackage(packageUrl, tempDirectoryName);
            string dir = Directory.GetCurrentDirectory();
            var fileMatchCount = Directory.EnumerateFiles(tempDirectoryName, targetFilename, SearchOption.AllDirectories).Count();
            Assert.IsTrue(fileMatchCount > 0);

            // do that again, this should not do anything since the cache already has the package
            PackageDownloader packageDownloader2 = await DownloadPackage(packageUrl, tempDirectoryName);
            fileMatchCount = Directory.EnumerateFiles(tempDirectoryName, targetFilename, SearchOption.AllDirectories).Count();
            Assert.IsTrue(fileMatchCount > 0);

            Assert.AreEqual(Directory.GetDirectories(tempDirectoryName).Count(), expectedCount);
            deleteTempDirs(packageDownloader1, tempDirectoryName);
        }

        /// <summary>
        /// Download the package
        /// </summary>
        /// <param name="packageUrl"></param>
        /// <param name="tempDirectoryName"></param>
        /// <returns></returns>
        private async Task<PackageDownloader> DownloadPackage(PackageURL packageUrl, string tempDirectoryName)
        {
            int numAttempts = 3;
            int numSecondsWait = 10;
            PackageDownloader packageDownloader = null;
            while (numAttempts-- > 0)
            {
                try
                {
                    packageDownloader = new PackageDownloader(packageUrl, tempDirectoryName);
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
