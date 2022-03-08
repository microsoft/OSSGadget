// Copyright (c) Microsoft Corporation. Licensed under the MIT License.


namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Helpers;
    using Model;
    using oss;
    using PackageManagers;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NPMProjectManagerTests
    {
        private readonly IDictionary<string, string> packages = new Dictionary<string, string>()
        {
            { "lodash", Resources.lodash_json },
            { "angular/core", Resources.angular_core_json },
            { "ds-modal", Resources.ds_modal_json },
            { "monorepolint", Resources.monorepolint_json },
            { "rly-cli", Resources.rly_cli_json },
        };

        private NPMProjectManager ProjectManager;
        
        public NPMProjectManagerTests()
        {
            ProjectManager = new NPMProjectManager(".");
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "Lodash modular utilities.")] // Normal package
        [DataRow("pkg:npm/angular/core@13.2.5", "Angular - the core framework")] // Scoped package
        [DataRow("pkg:npm/ds-modal@0.0.2", "")] // No Description at package level, and empty string description on version level
        [DataRow("pkg:npm/monorepolint@0.4.0")] // No Author property, and No Description
        [DataRow("pkg:npm/rly-cli@0.0.2", "RLY CLI allows you to setup fungilble SPL tokens and call Rally token programs from the command line.")] // Author property is an empty string
        public async Task MetadataSucceeds(string purlString, string? description = null)
        {
            PackageURL purl = new(purlString);
            PackageMetadata metadata = await ProjectManager.GetPackageMetadata(purl);

            string? packageName = purl.Namespace.IsNotBlank() ? $"@{purl.Namespace}/{purl.Name}" : purl.Name;
            Assert.AreEqual(packageName, metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
        }
    }
}
