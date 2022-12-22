// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Helpers;

using RecursiveExtractor;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public static class ArchiveHelper
{
    /// <summary>
    /// Logger for each of the subclasses
    /// </summary>
    static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Extracts an archive (given by 'bytes') into a directory named 'directoryName',
    /// recursively, using RecursiveExtractor.
    /// </summary>
    /// <param name="topLevelDirectory">The top level directory content should be extracted to.</param>
    /// <param name="directoryName">directory to extract content into (within <paramref name="topLevelDirectory"/>)</param>
    /// <param name="content">stream of the contents to extract (should be an archive file)</param>
    /// <param name="cached">If the archive has been cached.</param>
    /// <returns>The path that the archive was extracted to.</returns>
    public static async Task<string> ExtractArchiveAsync(
        string topLevelDirectory,
        string directoryName,
        Stream content,
        bool cached = false)
    {
        Logger.Trace("ExtractArchive({0}, <stream> len={1})", directoryName, content.Length);

        Directory.CreateDirectory(topLevelDirectory);

        StringBuilder dirBuilder = new(directoryName);

        foreach (char c in Path.GetInvalidPathChars())
        {
            dirBuilder.Replace(c, '-'); // CodeQL [cs/string-concatenation-in-loop] This is a small loop
        }

        // Extractor does not handle slashes well, so we need to remove them here.
        dirBuilder.Replace('/', '-');

        string fullTargetPath = Path.Combine(topLevelDirectory, dirBuilder.ToString());

        if (!cached)
        {
            while (Directory.Exists(fullTargetPath) || File.Exists(fullTargetPath))
            {
                dirBuilder.Append("-" + DateTime.Now.Ticks);
                fullTargetPath = Path.Combine(topLevelDirectory, dirBuilder.ToString());
            }
        }

        Extractor extractor = new();
        ExtractorOptions extractorOptions = new()
        {
            ExtractSelfOnFail = true, Parallel = true
            // MaxExtractedBytes = 1000 * 1000 * 10;  // 10 MB maximum package size
        };
        ExtractionStatusCode result = await extractor.ExtractToDirectoryAsync(topLevelDirectory, dirBuilder.ToString(),
            content, extractorOptions);
        if (result == ExtractionStatusCode.Ok)
        {
            Logger.Debug("Archive extracted to {0}", fullTargetPath);
        }
        else
        {
            Logger.Warn("Error extracting archive {0} ({1})", fullTargetPath, result);
        }

        return fullTargetPath;
    }
}