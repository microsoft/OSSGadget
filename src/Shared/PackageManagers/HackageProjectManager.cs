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

    internal class HackageProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "hackage";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_HACKAGE_ENDPOINT { get; set; } = "https://hackage.haskell.org";

        public HackageProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public HackageProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <summary>
        ///     Download one Hackage (Haskell) package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> n/a </returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            if (purl is null || purl.Name is null || purl.Version is null)
            {
                return Array.Empty<string>();
            }
            string packageName = purl.Name;
            string packageVersion = purl.Version;
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return Array.Empty<string>();
            }
            List<string> downloadedPaths = new();
            try
            {
                string url = $"{ENV_HACKAGE_ENDPOINT}/package/{packageName}-{packageVersion}/{packageName}-{packageVersion}.tar.gz";
                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage result = await httpClient.GetAsync(url);
                result.EnsureSuccessStatusCode();
                Logger.Debug("Downloading {0}...", purl.ToString());

                string targetName = $"hackage-{packageName}@{packageVersion}";
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
                Logger.Debug(ex, "Error downloading Hackage package: {0}", ex.Message);
            }
            return downloadedPaths;
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersionsAsync(PackageURL purl, bool useCache = true, bool includePrerelease = true)
        {
            Logger.Trace("EnumerateVersions {0}", purl?.ToString());
            if (purl == null || purl.Name is null)
            {
                return Array.Empty<string>();
            }

            try
            {
                string packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();

                System.Net.Http.HttpResponseMessage? html = await httpClient.GetAsync($"{ENV_HACKAGE_ENDPOINT}/package/{packageName}");
                html.EnsureSuccessStatusCode();
                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(await html.Content.ReadAsStringAsync());
                AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> ths = document.QuerySelectorAll("th");
                List<string> versionList = new();
                foreach (AngleSharp.Dom.IElement th in ths)
                {
                    if (th.TextContent.StartsWith("Versions"))
                    {
                        AngleSharp.Dom.IElement td = th.NextElementSibling;
                        foreach (AngleSharp.Dom.IElement version in td.QuerySelectorAll("a,strong"))
                        {
                            string versionString = version.TextContent.ToLower().Trim();
                            Logger.Debug("Identified {0} version {1}.", packageName, versionString);
                            versionList.Add(versionString);
                        }
                        break;
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
            if (purl is null || purl.Name is null)
            {
                return null;
            }
            try
            {
                string packageName = purl.Name;
                HttpClient httpClient = CreateHttpClient();

                return await GetHttpStringCache(httpClient, $"{ENV_HACKAGE_ENDPOINT}/package/{packageName}", useCache);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error fetching Hackage metadata: {ex.Message}");
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            return new Uri($"{ENV_HACKAGE_ENDPOINT}/package/{purl?.Name}");
        }
    }
}