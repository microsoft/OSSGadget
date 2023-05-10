// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using AngleSharp.Html.Parser;
    using Helpers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class CRANProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "cran";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_CRAN_ENDPOINT { get; set; } = "https://cran.r-project.org";

        public CRANProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public CRANProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one CRAN package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;

            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. All must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }

            HttpClient httpClient = CreateHttpClient();
            // Current Version
            try
            {
                string url = $"{ENV_CRAN_ENDPOINT}/src/contrib/{packageName}_{packageVersion}.tar.gz";
                System.Net.Http.HttpResponseMessage? result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                string targetName = $"cran-{packageName}@{packageVersion}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                if (doExtract && Directory.Exists(extractionPath) && cached == true)
                {
                    downloadedPaths.Add(extractionPath);
                    return downloadedPaths;
                }
                if (doExtract)
                {
                    downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsStreamAsync(), cached));
                }
                else
                {
                    extractionPath += Path.GetExtension(url) ?? "";
                    await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(extractionPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading CRAN package: {0}@{1}. Checking archives instead.", packageName, packageVersion);
            }
            if (downloadedPaths.Count > 0)
            {
                return downloadedPaths;
            }

            // Archive Version - Only continue here if needed
            try
            {
                string url = $"{ENV_CRAN_ENDPOINT}/src/contrib/Archive/{packageName}/{packageName}_{packageVersion}.tar.gz";
                System.Net.Http.HttpResponseMessage? result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl);

                string targetName = $"cran-{packageName}@{packageVersion}";
                string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
                if (doExtract)
                {
                    downloadedPaths.Add(await ArchiveHelper.ExtractArchiveAsync(TopLevelExtractionDirectory, targetName, await result.Content.ReadAsStreamAsync(), cached));
                }
                else
                {
                    extractionPath += Path.GetExtension(url) ?? "";
                    await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                    downloadedPaths.Add(extractionPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading CRAN package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null || purl.Name is null)
            {
                return new List<string>();
            }

            try
            {
                string packageName = purl.Name;
                List<string> versionList = new();
                HttpClient httpClient = CreateHttpClient();

                // Get the latest version
                System.Net.Http.HttpResponseMessage html = await httpClient.GetAsync($"{ENV_CRAN_ENDPOINT}/web/packages/{packageName}/index.html");
                html.EnsureSuccessStatusCode();
                HtmlParser? parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> tds = document.QuerySelectorAll("td");
                for (int i = 0; i < tds.Length; i++)
                {
                    if (tds[i].TextContent == "Version:")
                    {
                        string? value = tds[i + 1]?.TextContent?.Trim();
                        if (value != null)
                        {
                            versionList.Add(value);
                        }
                        break;
                    }
                }

                // Get the remaining versions
                html = await httpClient.GetAsync($"{ENV_CRAN_ENDPOINT}/src/contrib/Archive/{packageName}/");
                html.EnsureSuccessStatusCode();
                document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                tds = document.QuerySelectorAll("a");
                foreach (AngleSharp.Dom.IElement td in tds)
                {
                    string? href = td.GetAttribute("href");
                    if (href?.Contains(".tar.gz") ?? false)
                    {
                        string version = href.Replace(".tar.gz", "");
                        version = version.Replace(packageName + "_", "").Trim();
                        Logger.Debug("Identified {0} version {1}.", packageName, version);
                        versionList.Add(version);
                    }
                }
                return SortVersions(versionList.Distinct());
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Debug("Unable to enumerate versions (404): {0}", ex.Message);
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Logger.Debug("Unable to enumerate versions: {0}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadataAsync(PackageURL purl, bool useCache = true)
        {
            try
            {
                string? packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();
                string? content = await GetHttpStringCache(httpClient, $"{ENV_CRAN_ENDPOINT}/web/packages/{packageName}/index.html", useCache);
                return content;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching CRAN metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            // CRAN doesn't have a homepage for package version
            return new Uri($"{ENV_CRAN_ENDPOINT}/web/packages/{purl?.Name}/index.html");
        }
    }
}