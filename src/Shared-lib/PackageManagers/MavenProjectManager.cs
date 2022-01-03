// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Lib.PackageManagers
{
    using Lib;
    using Lib.PackageManagers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Xml;

    internal class MavenProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_MAVEN_ENDPOINT = "https://repo1.maven.org/maven2";

        public MavenProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public MavenProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        /// Download one Maven package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());
            string? packageNamespace = purl?.Namespace?.Replace('.', '/');
            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageNamespace) || string.IsNullOrWhiteSpace(packageName) ||
                string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Warn("Unable to download [{0} {1} {2}]. Both must be defined.", packageNamespace, packageName, packageVersion);
                return downloadedPaths;
            }

            try
            {
                string[] suffixes = new string[] { "-javadoc", "-sources", "" };
                foreach (string suffix in suffixes)
                {
                    string url = $"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{packageVersion}/{packageName}-{packageVersion}{suffix}.jar";
                    using HttpClient httpClient = CreateHttpClient();

                    System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                    result.EnsureSuccessStatusCode();
                    Logger.Debug($"Downloading {purl}...");

                    string targetName = $"maven-{packageNamespace}-{packageName}{suffix}@{packageVersion}";
                    targetName = targetName.Replace('/', '-');
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
                Logger.Warn(ex, "Error downloading Maven package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <summary>
        /// Check if the package exists in the respository.
        /// </summary>
        /// <param name="purl">The PackageURL to check.</param>
        /// <param name="useCache">If cache should be used.</param>
        /// <returns>True if the package is confirmed to exist in the repository. False otherwise.</returns>
        public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name) || string.IsNullOrEmpty(purl.Namespace))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageNamespace = purl.Namespace.Replace('.', '/');
            string packageName = purl.Name;
            using HttpClient httpClient = CreateHttpClient();

            return await CheckHttpCacheForPackage(httpClient, $"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/maven-metadata.xml", useCache);
        }

        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL? purl)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl is null || purl.Name is null || purl.Namespace is null)
            {
                return new List<string>();
            }
            try
            {
                string packageNamespace = purl.Namespace.Replace('.', '/');
                string packageName = purl.Name;
                using HttpClient httpClient = CreateHttpClient();

                string? content = await GetHttpStringCache(httpClient, $"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/maven-metadata.xml");
                List<string> versionList = new();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new List<string>();
                }
                XmlDocument doc = new();
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
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        public override async Task<string?> GetMetadata(PackageURL purl)
        {
            try
            {
                string? packageNamespace = purl?.Namespace?.Replace('.', '/');
                string? packageName = purl?.Name;
                using HttpClient httpClient = CreateHttpClient();
                if (purl?.Version == null)
                {
                    foreach (string? version in await EnumerateVersions(purl))
                    {
                        return await GetHttpStringCache(httpClient, $"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom");
                    }
                    throw new Exception("No version specified and unable to enumerate.");
                }
                else
                {
                    string version = purl.Version;
                    return await GetHttpStringCache(httpClient, $"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}/{version}/{packageName}-{version}.pom");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Error fetching Maven metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageNamespace = purl?.Namespace?.Replace('.', '/');
            string? packageName = purl?.Name;

            return new Uri($"{ENV_MAVEN_ENDPOINT}/{packageNamespace}/{packageName}");
        }
    }
}