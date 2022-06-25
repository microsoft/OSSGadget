// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using OpenSource.Helpers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ArchiveHelperTests
{
    /// <summary>
    /// Unit test that ExtractArchiveAsync works successfully.
    /// </summary>
    [TestMethod]
    public async Task ExtractArchiveSucceeds()
    {
        // Get the Base64Zip.zip file from our TestData resources.
        FileStream? zip = new(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TestData", "Base64Zip.zip"), FileMode.Open);
        
        // Create a list of the files we expect to be in the zip file. 
        string[] expectedFiles = {"Base64", "Hex", "oss-defog.dll"};

        // Create a new temporary directory
        string? targetDirectoryName = null;
        while (targetDirectoryName == null || Directory.Exists(targetDirectoryName))
        {
            targetDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        try
        {
            // Extract the zip file to the target directory.
            string? path = await ArchiveHelper.ExtractArchiveAsync(
                    targetDirectoryName,
                    "Base64Zip",
                    zip);
            
            // Assert that the directory we extracted the zip archive to exists.
            Assert.IsTrue(Directory.Exists(path));

            // Check that the extracted files from the zip archive match the ones we expected.
            string?[] files = Directory.EnumerateFiles(Path.Combine(path, "Base64Zip")).Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEquivalent(expectedFiles, files);
        }
        finally
        {
            // Delete the temp directory that was created.
            FileSystemHelper.RetryDeleteDirectory(targetDirectoryName);
            
            // Close the stream that was used to read the zip file.
            zip.Close();
        }
    }
}
