namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests
{
    using Contracts;
    using Extensions;
    using FluentAssertions;
    using Helpers;
    using Model;
    using Model.Metadata;
    using Moq;
    using Newtonsoft.Json;
    using oss;
    using PackageActions;
    using PackageManagers;
    using PackageUrl;
    using RichardSzalay.MockHttp;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using Xunit;

    public class NuGetProjectManagerV2Tests
    {
        private readonly IHttpClientFactory _httpFactory;

        private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
        {
            { "https://www.powershellgallery.com/api/v2/FindPackagesById()?id='psreadline'&semVerLevel=2.0.0", Resources.psreadline_xml },
        }.ToImmutableDictionary();

        // Map PackageURLs to metadata as json.
        private readonly IDictionary<string, string> _metadata = new Dictionary<string, string>()
        {
            { "pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_v2_4_1_beta_xml },
            { "pkg:nuget/PSReadLine?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_v2_4_1_beta_xml },
        }.ToImmutableDictionary();

        // Map PackageURLs to the list of versions as json.
        private readonly IDictionary<string, string> _versions = new Dictionary<string, string>()
        {
            { "pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_versions_json },
            { "pkg:nuget/PSReadLine?repository_url=https%3A//www.powershellgallery.com/api/v2/", Resources.psreadline_versions_json },
        }.ToImmutableDictionary();

        public NuGetProjectManagerV2Tests()
        {
            Mock<IHttpClientFactory> mockFactory = new();

            MockHttpMessageHandler mockHttp = new();

            foreach ((string url, string xml) in _packages)
            {
                MockHttpFetchResponse(HttpStatusCode.OK, url, xml, mockHttp);
            }

            mockHttp.When(HttpMethod.Get, "https://www.powershellgallery.com/api/v2/package/*").Respond(HttpStatusCode.OK);
            
            // Add mock setup for NuGet.org URLs
            mockHttp.When(HttpMethod.Get, "https://www.nuget.org/api/v2/package/*").Respond(HttpStatusCode.OK);
            mockHttp.When(HttpMethod.Get, "https://www.nuget.org/api/v2/FindPackagesById*").Respond(HttpStatusCode.OK);
            mockHttp.When(HttpMethod.Get, "https://www.nuget.org/api/v2/Packages*").Respond(HttpStatusCode.OK);

            // Set fallback to throw an exception for unmatched requests
            mockHttp.Fallback.Throw(new InvalidOperationException("Unmatched HTTP request. Ensure all requests are properly mocked."));

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
            _httpFactory = mockFactory.Object;
        }

        /// <summary>
        /// Creates a repository-specific NuGetV2ProjectManager based on the PackageURL's repository_url qualifier.
        /// </summary>
        /// <param name="purl">The PackageURL to extract the repository URL from.</param>
        /// <returns>A new NuGetV2ProjectManager configured for the specified repository.</returns>
        private NuGetV2ProjectManager CreateRepositorySpecificProjectManager(PackageURL purl)
        {
            var repositoryUrl = purl.GetRepositoryUrlOrDefault(NuGetV2ProjectManager.POWER_SHELL_GALLERY_DEFAULT_INDEX) ?? NuGetV2ProjectManager.POWER_SHELL_GALLERY_DEFAULT_INDEX;
            return new NuGetV2ProjectManager(".", NuGetPackageActions.CreateV2(repositoryUrl), _httpFactory);
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", "pkg:nuget/PSReadLine?repository_url=https%3A//www.powershellgallery.com/api/v2/")]
        public void TestPackageUrlEncoding(string input, string expected)
        {
            PackageURL purl = new(input);
            string actual = purl.ToString();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/")]
        [InlineData("pkg:nuget/psreadline@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/")]
        [InlineData("pkg:nuget/Az.Accounts@4.0.0?repository_url=https://www.powershellgallery.com/api/v2/")]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/")]
        [InlineData("pkg:nuget/newtonsoft.json@12.0.3?repository_url=https://www.nuget.org/api/v2/")]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/")]
        public async Task TestNugetCaseInsensitiveHandlingPackageExistsSucceeds(string purlString)
        {
            PackageURL purl = new(purlString);

            // Create repository-specific project manager
            var projectManager = CreateRepositorySpecificProjectManager(purl);

            bool exists = await projectManager.PackageVersionExistsAsync(purl, useCache: false);

            Assert.True(exists);
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [InlineData("pkg:nuget/NonExistentPackageXYZ@0.0.0?repository_url=https://www.powershellgallery.com/api/v2/", false)]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/", true)]
        [InlineData("pkg:nuget/NonExistentPackageXYZ@0.0.0?repository_url=https://www.nuget.org/api/v2/", false)]
        public async Task PackageExistsSucceeds(string purlString, bool expected)
        {
            PackageURL purl = new(purlString);

            // Create repository-specific project manager
            var projectManager = CreateRepositorySpecificProjectManager(purl);

            bool exists = await projectManager.PackageVersionExistsAsync(purl, useCache: false);

            exists.Should().Be(expected);
        }

        [Theory]
        [InlineData("pkg:nuget/Az.Accounts@4.0.0?repository_url=https://www.powershellgallery.com/api/v2/", "https://www.powershellgallery.com/api/v2/package/az.accounts/4.0.0")]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/", "https://www.nuget.org/api/v2/package/newtonsoft.json/12.0.3")]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/", "https://www.nuget.org/api/v2/package/microsoft.extensions.logging/8.0.0")]
        public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedNuPkgUrl)
        {
            PackageURL purl = new(purlString);

            // Create repository-specific project manager
            var projectManager = CreateRepositorySpecificProjectManager(purl);

            var uris = projectManager.GetArtifactDownloadUrisAsync(purl);

            var nupkgArtifactUri = await uris
                .FirstAsync(it => it.Type == BaseNuGetProjectManager.NuGetArtifactType.Nupkg);

            Assert.Equal(expectedNuPkgUrl, nupkgArtifactUri.Uri.ToString());
            Assert.True(await projectManager.UriExistsAsync(nupkgArtifactUri.Uri));
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", false, "Great command line editing in the PowerShell console host")]
        [InlineData("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", true, "Great command line editing in the PowerShell console host", "2.4.1-beta1")]
        public async Task MetadataSucceeds_Mocked_PowerShellGallery(string purlString, bool includePrerelease = false, string? description = null, string? latestVersion = null)
        {
            PackageURL purl = new(purlString);

            NuGetPackageVersionMetadata? setupMetadata = null;
            IEnumerable<string>? setupVersions = null;

            if (_metadata.TryGetValue(purl.ToString(), out string? setupMetadataString))
            {
                NuGetV2PackageVersionMetadata nuGetV2PackageVersionMetadata = DeserializeFromXml<NuGetV2PackageVersionMetadata>(setupMetadataString);
                setupMetadata = new NuGetPackageVersionMetadata(nuGetV2PackageVersionMetadata);
            }

            if (_versions.TryGetValue(purl.ToString(), out string? setupVersionsString))
            {
                setupVersions = JsonConvert.DeserializeObject<IEnumerable<string>>(setupVersionsString);
            }

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                setupMetadata,
                setupVersions?.Reverse());

            // Use mocked response if version is not provided.
            var projectManager = string.IsNullOrWhiteSpace(purl.Version) ? new NuGetV2ProjectManager(".", nugetPackageActions, _httpFactory) : CreateRepositorySpecificProjectManager(purl);

            PackageMetadata? metadata = await projectManager.GetPackageMetadataAsync(purl, includePrerelease: includePrerelease, useCache: false);

            Assert.NotNull(metadata);
            Assert.Equal(purl.Name, metadata!.Name, ignoreCase: true);

            // If a version was specified, assert the response is for this version, otherwise assert for the latest version.
            Assert.Equal(!string.IsNullOrWhiteSpace(purl.Version) ? purl.Version : latestVersion,
                metadata.PackageVersion);
            Assert.Equal(description, metadata.Description);
        }

        [Theory]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/", false, "Json.NET is a popular high-performance JSON framework for .NET")]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/", false, "Logging infrastructure default implementation for Microsoft.Extensions.Logging.")]
        public async Task MetadataSucceeds_Live_NuGetOrg(string purlString, bool includePrerelease = false, string? expectedDescription = null)
        {
            PackageURL purl = new(purlString);

            // Create repository-specific project manager for live API calls
            var projectManager = CreateRepositorySpecificProjectManager(purl);

            PackageMetadata? metadata = await projectManager.GetPackageMetadataAsync(purl, includePrerelease: includePrerelease, useCache: false);

            Assert.Equal(purl.Name, metadata!.Name);
            Assert.Equal(purl.Version, metadata.PackageVersion);
            Assert.Equal(expectedDescription, metadata.Description);
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", 45, "2.4.1-beta1")]
        [InlineData("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", 45, "2.4.1-beta1")]
        public async Task EnumerateVersionsSucceeds_Mocked_PowerShellGallery(
            string purlString,
            int count,
            string? latestVersion,
            bool includePrerelease = true)
        {
            PackageURL purl = new(purlString);
            NuGetPackageVersionMetadata? setupMetadata = null;
            IEnumerable<string>? setupVersions = null;

            if (_metadata.TryGetValue(purl.ToString(), out string? setupMetadataString))
            {
                NuGetV2PackageVersionMetadata nuGetV2PackageVersionMetadata = DeserializeFromXml<NuGetV2PackageVersionMetadata>(setupMetadataString);
                setupMetadata = new NuGetPackageVersionMetadata(nuGetV2PackageVersionMetadata);
            }

            if (_versions.TryGetValue(purl.ToString(), out string? setupVersionsString))
            {
                setupVersions = JsonConvert.DeserializeObject<IEnumerable<string>>(setupVersionsString);
            }

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                purl,
                setupMetadata,
                setupVersions?.Reverse(),
                includePrerelease: includePrerelease);
            var projectManager = new NuGetV2ProjectManager(".", nugetPackageActions, _httpFactory);

            List<string> versions = (await projectManager.EnumerateVersionsAsync(purl, false, includePrerelease)).ToList();

            Assert.Equal(count, versions.Count);
            Assert.Equal(latestVersion, versions.FirstOrDefault());
        }

        [Theory]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/", false)]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/", false)]
        [InlineData("pkg:nuget/Newtonsoft.Json?repository_url=https://www.nuget.org/api/v2/", true)]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging?repository_url=https://www.nuget.org/api/v2/", true)]
        public async Task EnumerateVersionsSucceeds_Live_NuGetOrg(string purlString, bool includePrerelease = false)
        {
            PackageURL purl = new(purlString);

            // Create repository-specific project manager for live API calls
            var projectManager = CreateRepositorySpecificProjectManager(purl);

            List<string> versions = (await projectManager.EnumerateVersionsAsync(purl, false, includePrerelease)).ToList();

            Assert.True(versions.Count > 0, "Should return at least one version");
            Assert.All(versions, version => Assert.False(string.IsNullOrWhiteSpace(version)));
            
            // For packages with a specific version, verify that version is in the list
            if (!string.IsNullOrWhiteSpace(purl.Version))
            {
                Assert.Contains(purl.Version, versions);
            }
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.0.0?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [InlineData("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", false)] // no version provided
        [InlineData("pkg:nuget/Az.Accounts@2.5.3?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [InlineData("pkg:nuget/PowerShellGet@2.2.5?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [InlineData("pkg:nuget/notarealpackage@0.0.0?repository_url=https://www.powershellgallery.com/api/v2/", false)] // not a real package
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/", true)]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/", true)]
        [InlineData("pkg:nuget/nonexistentpackage@0.0.0?repository_url=https://www.nuget.org/api/v2/", false)] // not a real package
        public async Task DetailedPackageVersionExistsAsync_ExistsSucceeds(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);

            // Create repository-specific project manager
            var projectManager = CreateRepositorySpecificProjectManager(purl);

            IPackageExistence existence = await projectManager.DetailedPackageVersionExistsAsync(purl, useCache: false);

            Assert.Equal(exists, existence.Exists);
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.0.0?repository_url=https://www.powershellgallery.com/api/v2/", "2020-02-11T18:22:59.793")]
        [InlineData("pkg:nuget/Az.Accounts@2.5.3?repository_url=https://www.powershellgallery.com/api/v2/", "2021-09-07T05:57:05.487")]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/", "2019-11-09T01:27:30.723")]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/", "2023-11-14T13:23:26.98")]
        public async Task GetPublishedAtSucceeds(string purlString, string expectedTime)
        {
            PackageURL purl = new(purlString);
            
            // Create repository-specific project manager
            var projectManager = CreateRepositorySpecificProjectManager(purl);
            
            DateTime? time = await projectManager.GetPublishedAtAsync(purl, useCache: false);

            Assert.Equal(DateTime.Parse(expectedTime), time);
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/")]
        [InlineData("pkg:nuget/Az.Accounts?repository_url=https://www.powershellgallery.com/api/v2/")]
        [InlineData("pkg:nuget/Microsoft.Graph.Authentication?repository_url=https://www.powershellgallery.com/api/v2/")]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/")]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/")]
        [InlineData("pkg:nuget/System.Text.Json?repository_url=https://www.nuget.org/api/v2/")]
        public async Task GetPackagePrefixReserved_ReturnsFalse(string purlString)
        {
            PackageURL purl = new(purlString);
            var projectManager = new NuGetV2ProjectManager(".", null, _httpFactory);
            bool isReserved = await projectManager.GetHasReservedNamespaceAsync(purl, useCache: false);

            Assert.False(isReserved); // Reserved namespaces are not supported in NuGet V2
        }

        [Theory]
        [InlineData("pkg:nuget/PSReadLine@2.4.1-beta1?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [InlineData("pkg:nuget/PSReadLine?repository_url=https://www.powershellgallery.com/api/v2/", true)]
        [InlineData("pkg:nuget/notarealpackage?repository_url=https://www.powershellgallery.com/api/v2/", false)]
        public async Task DetailedPackageExistsAsync_Succeeds_WithMockedData(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions;

            if (exists)
            {
                var setupMetadataString = _metadata[purl.ToString()];
                NuGetV2PackageVersionMetadata nuGetV2PackageVersionMetadata = DeserializeFromXml<NuGetV2PackageVersionMetadata>(setupMetadataString);
                var setupMetadata = new NuGetPackageVersionMetadata(nuGetV2PackageVersionMetadata);
                // If we expect the package to exist, setup the helper as such.
                nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                    purl,
                    setupMetadata,
                    JsonConvert.DeserializeObject<IEnumerable<string>>(_versions[purl.ToString()])?.Reverse());
            }
            else
            {
                // If we expect the package to not exist, mock the actions to not do anything.
                nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions();
            }

            var projectManager = new NuGetV2ProjectManager(".", nugetPackageActions, _httpFactory);

            IPackageExistence existence = await projectManager.DetailedPackageExistsAsync(purl, useCache: false);

            Assert.Equal(exists, existence.Exists);
        }

        [Theory]
        [InlineData("pkg:nuget/Newtonsoft.Json@12.0.3?repository_url=https://www.nuget.org/api/v2/", true)]
        [InlineData("pkg:nuget/Microsoft.Extensions.Logging@8.0.0?repository_url=https://www.nuget.org/api/v2/", true)]
        [InlineData("pkg:nuget/nonexistentpackage123xyz@0.0.0?repository_url=https://www.nuget.org/api/v2/", false)]
        public async Task DetailedPackageExistsAsync_NuGetOrg(string purlString, bool exists)
        {
            PackageURL purl = new(purlString);

            var projectManager = CreateRepositorySpecificProjectManager(purl);

            IPackageExistence existence = await projectManager.DetailedPackageExistsAsync(purl, useCache: false);

            Assert.Equal(exists, existence.Exists);
        }

        private static void MockHttpFetchResponse(
            HttpStatusCode statusCode,
            string url,
            string content,
            MockHttpMessageHandler httpMock)
        {
            httpMock
                .When(HttpMethod.Get, url)
                .Respond(statusCode, "application/xml", content);
        }

        public static T DeserializeFromXml<T>(string xml)
        {
            XmlSerializer serializer = new(typeof(T));
            using StringReader reader = new(xml);
            object? result = serializer.Deserialize(reader) ?? throw new InvalidOperationException("Deserialization returned null.");
            return (T)result;
        }
    }
}
