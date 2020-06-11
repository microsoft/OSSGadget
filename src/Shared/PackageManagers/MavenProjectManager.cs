// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class MavenProjectManager : BaseProjectManager
    {
        #region Public Fields

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_MAVEN_ENDPOINT = "https://repo1.maven.org/maven2";

        #endregion Public Fields

        #region Public Constructors

        public MavenProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        #endregion Public Constructors

        #region Public Methods

        /// <summary>
        /// Download one Maven package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            var packageNamespace = purl?.Namespace?.Replace('.', '/');
            var packageName = purl?.Name;
            var packageVersion = purl?.Version;
            var downloadedPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName) ||
                string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Error("Unable to download [{0} {1} {2}]. Both must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                var suffixes = new string[] { "-javadoc", "-sources", "" };
                foreach (var suffix in suffixes)
                {
                    var url = $"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{packageVersion}/{packageName}-{packageVersion}{suffix}.jar";
                    var result = await WebClient.GetAsync(url);
                    result.EnsureSuccessStatusCode();
                    Logger.Debug($"Downloading {purl}...");

                    var targetName = $"maven-{packageNamespace}/{packageName}{suffix}@{packageVersion}";
                    string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                    if (doExtract && Directory.Exists(extractionPath) && cached == true)
                    {
                        downloadedPaths.Add(extractionPath);
                        return downloadedPaths;
                    }
                    if (doExtract)
                    {
                        downloadedPaths.Add(await ExtractArchive(targetName, await result.Content.ReadAsByteArrayAsync(), cached));
                    }
                    else
                    {
                        targetName += Path.GetExtension(url) ?? "";
                        await File.WriteAllBytesAsync(targetName, await result.Content.ReadAsByteArrayAsync());
                        downloadedPaths.Add(targetName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error downloading Maven package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL? purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null)
            {
                return new List<string>();
            }
            try
            {
                var packageNamespace = purl?.Namespace?.Replace('.', '/');
                var packageName = purl?.Name;
                var content = await GetHttpStringCache($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/maven-metadata.xml");
                var versionList = new List<string>();

                var doc = new XmlDocument();
                doc.LoadXml(content);
                foreach (XmlNode? versionObject in doc.GetElementsByTagName("version"))
                {
                    if (versionObject != null)
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, versionObject.InnerText);
                        versionList.Add(versionObject.InnerText);
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error enumerating Maven packages: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                var packageNamespace = purl?.Namespace?.Replace('.', '/');
                var packageName = purl?.Name;
                if (purl?.Version == null)
                {
                    foreach (var version in await EnumerateVersions(purl))
                    {
                        return await GetHttpStringCache($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom");
                    }
                    throw new Exception("No version specified and unable to enumerate.");
                }
                else
                {
                    var version = purl.Version;
                    return await GetHttpStringCache($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching Maven metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            var packageNamespace = purl?.Namespace?.Replace('.', '/');
            var packageName = purl?.Name;

            return new Uri($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}");
        }

        #endregion Public Methods
    }
}