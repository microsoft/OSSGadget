// Copyright (c) Microsoft Corporation. Licensed under the MIT License.


namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Model;
    using Model.Providers;
    using Moq;
    using OpenSource.Helpers;
    using oss;
    using PackageManagers;
    using PackageUrl;
    using RichardSzalay.MockHttp;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NPMProjectManagerTests
    {
        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://registry.npmjs.org/lodash", Resources.lodash_json },
            { "https://registry.npmjs.org/@angular/core", Resources.angular_core_json },
            { "https://registry.npmjs.org/ds-modal", Resources.ds_modal_json },
            { "https://registry.npmjs.org/monorepolint", Resources.monorepolint_json },
            { "https://registry.npmjs.org/rly-cli", Resources.rly_cli_json },
            { "https://registry.npmjs.org/example", Resources.minimum_json_json },
        };

        private readonly NPMProjectManager _projectManager;
        
        public NPMProjectManagerTests()
        {
            Mock<IHttpClientFactory> mockFactory = new();
            
            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string json) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
            }
 
            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            
            _projectManager = new NPMProjectManager(mockFactory.Object, new BaseProvider(), ".");
        }

        [DataTestMethod]
        [DataRow("pkg:npm/lodash@4.17.15", "Lodash modular utilities.")] // Normal package
        [DataRow("pkg:npm/angular/core@13.2.5", "Angular - the core framework")] // Scoped package
        [DataRow("pkg:npm/ds-modal@0.0.2", "")] // No Description at package level, and empty string description on version level
        [DataRow("pkg:npm/monorepolint@0.4.0")] // No Author property, and No Description
        [DataRow("pkg:npm/example@0.0.0")] // Pretty much only name, and version
        [DataRow("pkg:npm/rly-cli@0.0.2", "RLY CLI allows you to setup fungilble SPL tokens and call Rally token programs from the command line.")] // Author property is an empty string
        public async Task MetadataSucceeds(string purlString, string? description = null)
        {
            PackageURL purl = new(purlString);
            PackageMetadata metadata = await _projectManager.GetPackageMetadata(purl, useCache: false);

            string? packageName = purl.Namespace.IsNotBlank() ? $"@{purl.Namespace}/{purl.Name}" : purl.Name;
            Assert.AreEqual(packageName, metadata.Name);
            Assert.AreEqual(purl.Version, metadata.PackageVersion);
            Assert.AreEqual(description, metadata.Description);
        }
        
        private static void MockHttpFetchResponse(
            HttpStatusCode statusCode,
            string url,
            string content,
            MockHttpMessageHandler httpMock)
        {
            httpMock
                .When(HttpMethod.Get, url)
                .Respond(statusCode, "application/json", content);
        }
    }
}
