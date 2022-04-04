// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Extensions;
    using Helpers;
    using Model.Metadata;
    using Model.PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class CargoProjectManager : BaseProjectManager
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CARGO_ENDPOINT = "https://crates.io";
        
        private IManagerPackageActions<CargoPackageVersionMetadata> _actions;
        public CargoProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory, IManagerPackageActions<CargoPackageVersionMetadata>? actions = null) : base(httpClientFactory, destinationDirectory)
        {
            _actions = actions ?? new CargoPackageActions(httpClientFactory);
        }

        public CargoProjectManager(string destinationDirectory) : base(destinationDirectory)
        {
            _actions = new CargoPackageActions();
        }

        /// <summary>
        ///     Download one Cargo package and extract it to the target directory.
        /// </summary>
        /// <param name="purl"> Package URL of the package to download. </param>
        /// <returns> Path to the downloaded package </returns>
        public override async Task<IEnumerable<string>> DownloadVersion(PackageURL purl, bool doExtract, bool cached = false)
        {
            // This could be simplified.
            ArgumentNullException.ThrowIfNull(purl, nameof(purl));
            Logger.Trace("DownloadVersion {0}", purl.ToString());
            string? fileName = purl.ToStringFilename();
            string targetName = $"cargo-{fileName}";
            string extractionPath = Path.Combine(TopLevelExtractionDirectory, targetName);
            string? containingPath = await _actions.DownloadAsync(purl, TopLevelExtractionDirectory, extractionPath, doExtract, cached);
            
            if (containingPath is string notNullPath)
            {
                return Directory.EnumerateFiles(notNullPath, "*",
                    new EnumerationOptions() {RecurseSubdirectories = true});
            }

            return Array.Empty<string>();
        }

        /// <inheritdoc />
        public override async Task<bool> PackageExists(PackageURL purl, bool useCache = true)
        {
            ArgumentNullException.ThrowIfNull(purl, nameof(purl));
            Logger.Trace("PackageExists {0}", purl.ToString());
            return await _actions.DoesPackageExistAsync(purl, useCache);
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<string>> EnumerateVersions(PackageURL purl, bool useCache = true)
        {
            ArgumentNullException.ThrowIfNull(purl, nameof(purl));
            Logger.Trace("EnumerateVersions {0}", purl.ToString());
            return await _actions.GetAllVersionsAsync(purl, useCache);
        }

        /// <inheritdoc />
        public override async Task<string?> GetMetadata(PackageURL purl, bool useCache = true)
        {
            return (await _actions.GetMetadataAsync(purl, useCache)).ToString();
        }

        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageName = purl?.Name;
            return new Uri($"{ENV_CARGO_ENDPOINT}/crates/{packageName}");
            // TODO: Add version support
        }
    }
}