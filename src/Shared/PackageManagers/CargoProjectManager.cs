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

    internal class CargoProjectManager : TypedManager<CargoPackageVersionMetadata>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public static string ENV_CARGO_ENDPOINT = "https://crates.io";

        public CargoProjectManager(string directory, IManagerPackageActions<CargoPackageVersionMetadata>? actions = null) : base(actions ?? new CargoPackageActions(), directory)
        {
        }
        
        public override Uri GetPackageAbsoluteUri(PackageURL purl)
        {
            string? packageName = purl?.Name;
            return new Uri($"{ENV_CARGO_ENDPOINT}/crates/{packageName}");
            // TODO: Add version support
        }
    }
}