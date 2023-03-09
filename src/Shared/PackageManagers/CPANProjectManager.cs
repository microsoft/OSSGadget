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
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class CPANProjectManager : BaseProjectManager
    {
        /// <summary>
        /// The type of the project manager from the package-url type specifications.
        /// </summary>
        /// <seealso href="https://www.github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst"/>
        public const string Type = "cpan";

        public override string ManagerType => Type;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_CPAN_BINARY_ENDPOINT { get; set; } = "https://cpan.metacpan.org";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_CPAN_ENDPOINT { get; set; } = "https://metacpan.org";
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_CPAN_API_ENDPOINT { get; set; } = "https://fastapi.metacpan.org";

        public CPANProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory) : base(httpClientFactory, destinationDirectory)
        {
        }

        public CPANProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
        }

        /// <inheritdoc />
        public override async Task<bool> PackageExistsAsync(PackageURL purl, bool useCache = true)
        {
            Logger.Trace("PackageExists {0}", purl?.ToString());
            if (string.IsNullOrEmpty(purl?.Name))
            {
                Logger.Trace("Provided PackageURL was null.");
                return false;
            }
            string packageName = purl.Name;
            HttpClient httpClient = CreateHttpClient();
            return await CheckHttpCacheForPackage(httpClient, $"{ENV_CPAN_ENDPOINT}/release/{packageName}", useCache);
        }

        /// <summary>
        /// Download one CPAN package and extract it to the target directory.
        /// </summary>
        /// <param name="purl">Package URL of the package to download.</param>
        /// <returns>n/a</returns>
        public override async Task<IEnumerable<string>> DownloadVersionAsync(PackageURL purl, bool doExtract, bool cached = false)
        {
            Logger.Trace("DownloadVersion {0}", purl?.ToString());

            string? packageName = purl?.Name;
            string? packageVersion = purl?.Version;
            List<string> downloadedPaths = new();

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(packageVersion))
            {
                Logger.Debug("Unable to download [{0} {1}]. Both must be defined.", packageName, packageVersion);
                return downloadedPaths;
            }
            // Locate the URL
            HttpClient httpClient = CreateHttpClient();
            string packageVersionUrl = $"{ENV_CPAN_API_ENDPOINT}/v1/release/{packageName}";
            string? releaseContent = await GetHttpStringCache(httpClient, packageVersionUrl);
            if (string.IsNullOrEmpty(releaseContent)) { return downloadedPaths; }

            Logger.Debug($"Downloading from {packageVersionUrl}");

            JsonDocument contentJSON = JsonDocument.Parse(releaseContent);
            JsonElement root = contentJSON.RootElement;

            string binaryUrl = root.GetProperty("download_url").GetString();

            HttpResponseMessage result = await httpClient.GetAsync(binaryUrl);
            result.EnsureSuccessStatusCode();
            Logger.Debug("Downloading {0}...", purl);

            string targetName = $"cpan-{packageName}@{packageVersion}";
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
                extractionPath += Path.GetExtension(binaryUrl) ?? "";
                await File.WriteAllBytesAsync(extractionPath, await result.Content.ReadAsByteArrayAsync());
                downloadedPaths.Add(extractionPath);
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

                string? html = await GetHttpStringCache(httpClient, $"{ENV_CPAN_ENDPOINT}/release/{packageName}");
                HtmlParser parser = new();
                AngleSharp.Html.Dom.IHtmlDocument document = await parser.ParseDocumentAsync(html);
                foreach (AngleSharp.Dom.IElement option in document.QuerySelectorAll("div.release select.extend option"))
                {
                    if (!option.HasAttribute("value"))
                    {
                        continue;
                    }
                    string? value = option.GetAttribute("value");
                    Match? match = Regex.Match(value, @".*-([^-]+)$");
                    if (match.Success)
                    {
                        Logger.Debug("Identified {0} version {1}.", packageName, match.Groups[1].Value);
                        versionList.Add(match.Groups[1].Value);
                    }
                }

                IEnumerable<string> result = SortVersions(versionList.Distinct());
                return result;
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
                if (packageName != null)
                {
                    HttpClient httpClient = CreateHttpClient();
                    string? contentRelease = await GetHttpStringCache(httpClient, $"{ENV_CPAN_ENDPOINT}/release/{packageName}", useCache);
                    string? contentPod = await GetHttpStringCache(httpClient, $"{ENV_CPAN_ENDPOINT}/pod/{packageName.Replace("-", "::")}", useCache);
                    return contentRelease + "\n" + contentPod;
                }
                else
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error fetching CPAN metadata: {0}", ex.Message);
                return null;
            }
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageName = purl?.Name;
            return new Uri($"{ENV_CPAN_ENDPOINT}/pod/{packageName}");
            // TODO: Add version support
        }
    }
}