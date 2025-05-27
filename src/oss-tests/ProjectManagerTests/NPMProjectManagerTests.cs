// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests;

using FluentAssertions;
using Microsoft.CST.OpenSource.Contracts;
using Microsoft.CST.OpenSource.Extensions;
using Microsoft.CST.OpenSource.Model;
using Microsoft.CST.OpenSource.Model.Enums;
using Microsoft.CST.OpenSource.Model.PackageExistence;
using Microsoft.CST.OpenSource.PackageActions;
using Microsoft.CST.OpenSource.PackageManagers;
using Moq;
using oss;
using PackageUrl;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class NPMProjectManagerTests
{
    private static readonly IDictionary<string, (IPackageExistence packageVersionExistence, bool versionPulled, bool packagePulled, bool consideredMalicious)> _packageVersionExistence = new Dictionary<string, (IPackageExistence packageVersionExistence, bool versionPulled, bool packagePulled, bool consideredMalicious)>()
    {
        { "currently_exists",
            (new PackageVersionExists(),
                versionPulled: false,
                packagePulled: false,
                consideredMalicious: false)
        },
        { "version_pulled",
            (new PackageVersionRemoved(new HashSet<PackageVersionRemovalReason>(new[] { PackageVersionRemovalReason.VersionUnpublished })),
                versionPulled: true,
                packagePulled: false,
                consideredMalicious: false)
        },
        { "version_never_existed",
            (new PackageVersionNotFound(),
                versionPulled: false,
                packagePulled: false,
                consideredMalicious: false)
        },
        { "package_considered_malicious",
            (new PackageVersionRemoved(new HashSet<PackageVersionRemovalReason>(new[] { PackageVersionRemovalReason.RemovedByRepository })),
                versionPulled: false,
                packagePulled: false,
                consideredMalicious: true)
        }
    };
    
    private static readonly IDictionary<string, (IPackageExistence packageExistence, bool pulled, bool consideredMalicious)> _packageExistence = new Dictionary<string, (IPackageExistence packageExistence, bool pulled, bool consideredMalicious)>()
    {
        { "currently_exists",
            (new PackageExists(),
                pulled: false,
                consideredMalicious: false)
        },
        { "never_existed",
            (new PackageNotFound(),
                pulled: false,
                consideredMalicious: false)
        },
        { "pulled",
            (new PackageRemoved(new HashSet<PackageRemovalReason>(new[] { PackageRemovalReason.PackageUnpublished })),
                pulled: true,
                consideredMalicious: false)
        },
        { "considered_malicious",
            (new PackageRemoved(new HashSet<PackageRemovalReason>(new[] { PackageRemovalReason.RemovedByRepository })),
                pulled: false,
                consideredMalicious: true)
        },
    };

    private readonly IDictionary<string, string> _packages = new Dictionary<string, string>()
    {
        { "https://registry.npmjs.org/lodash", Resources.lodash_json },
        { "https://registry.npmjs.org/lodash.js", Resources.lodashjs_json },
        { "https://registry.npmjs.org/%40somosme/webflowutils", Resources.unpublishedpackage_json },
        { "https://registry.npmjs.org/%40angular/core", Resources.angular_core_json },
        { "https://registry.npmjs.org/%40achievementify/client", Resources.achievementify_client_json },
        { "https://registry.npmjs.org/ds-modal", Resources.ds_modal_json },
        { "https://registry.npmjs.org/monorepolint", Resources.monorepolint_json },
        { "https://registry.npmjs.org/rly-cli", Resources.rly_cli_json },
        { "https://registry.npmjs.org/tslib", Resources.tslib_json },
        { "https://registry.npmjs.org/example", Resources.minimum_json_json },
    }.ToImmutableDictionary();

    private readonly IDictionary<string, string> _packageVersions = new Dictionary<string, string>()
    {
        { "https://registry.npmjs.org/lodash/4.17.15", "mockContent" },
        { "https://registry.npmjs.org/@angular/core/13.2.5","mockContent" },
        { "https://registry.npmjs.org/ds-modal/0.0.2", "mockContent" },
        { "https://registry.npmjs.org/monorepolint/0.4.0", "mockContent" },
        { "https://registry.npmjs.org/example/0.0.0", "mockContent" },
        { "https://registry.npmjs.org/rly-cli/0.0.2", "mockContent" },
        { "https://registry.npmjs.org/lodash.js/0.0.1-security", "mockContent" },
        { "https://registry.npmjs.org/tslib/2.4.1", "mockContent" },
    }.ToImmutableDictionary();
    
    private readonly IDictionary<string, string> _packageOwners = new Dictionary<string, string>()
    {
        { "https://registry.npmjs.org/-/user/jdalton/package", Resources.jdalton_packages_json },
        { "https://registry.npmjs.org/-/user/microsoft/package", Resources.microsoft_packages_json },
        { "https://registry.npmjs.org/-/user/azure/package", Resources.azure_packages_json },
    }.ToImmutableDictionary();

    private readonly Mock<NPMProjectManager> _projectManager;
    private readonly IHttpClientFactory _httpFactory;
    
    public NPMProjectManagerTests()
    {
        Mock<IHttpClientFactory> mockFactory = new();
        
        MockHttpMessageHandler mockHttp = new();

        foreach ((string url, string json) in _packages)
        {
            MockHttpFetchResponse(HttpStatusCode.OK, url, json, mockHttp);
        }

        foreach ((string url, string content) in _packageVersions)
        {
            MockHttpFetchResponse(HttpStatusCode.OK, url, content, mockHttp);
        }

        foreach ((string url, string content) in _packageOwners)
        {
            MockHttpFetchResponse(HttpStatusCode.OK, url, content, mockHttp);
        }

        mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
        _httpFactory = mockFactory.Object;

        _projectManager = new Mock<NPMProjectManager>(".", new NoOpPackageActions(), _httpFactory, null) { CallBase = true };
    }

    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15", "Lodash modular utilities.")] // Normal package
    [InlineData("pkg:npm/%40angular/core@13.2.5", "Angular - the core framework")] // Scoped package
    [InlineData("pkg:npm/ds-modal@0.0.2", "")] // No Description at package level, and empty string description on version level
    [InlineData("pkg:npm/monorepolint@0.4.0")] // No Author property, and No Description
    [InlineData("pkg:npm/example@0.0.0")] // Pretty much only name, and version
    [InlineData("pkg:npm/rly-cli@0.0.2", "RLY CLI allows you to setup fungilble SPL tokens and call Rally token programs from the command line.")] // Author property is an empty string
    public async Task MetadataSucceeds(string purlString, string? description = null)
    {
        PackageURL purl = new(purlString);
        PackageMetadata? metadata = await _projectManager.Object.GetPackageMetadataAsync(purl, useCache: false);

        Assert.NotNull(metadata);
        Assert.Equal(purl.GetFullName(), metadata.Name);
        Assert.Equal(purl.Version, metadata.PackageVersion);
        Assert.Equal(description, metadata.Description);
    }

    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15", 114, "4.17.21")]
    [InlineData("pkg:npm/%40angular/core@13.2.5", 566, "13.2.6")]
    [InlineData("pkg:npm/ds-modal@0.0.2", 3, "0.0.2")]
    [InlineData("pkg:npm/monorepolint@0.4.0", 88, "0.4.0")]
    [InlineData("pkg:npm/example@0.0.0", 1, "0.0.0")]
    [InlineData("pkg:npm/rly-cli@0.0.2", 4, "0.0.4")]
    public async Task EnumerateVersionsSucceeds(string purlString, int count, string latestVersion)
    {
        PackageURL purl = new(purlString);
        List<string> versions = (await _projectManager.Object.EnumerateVersionsAsync(purl, useCache: false)).ToList();

        Assert.Equal(count, versions.Count);
        Assert.Equal(latestVersion, versions.First());
    }

    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15")]
    [InlineData("pkg:npm/%40angular/core@13.2.5")]
    [InlineData("pkg:npm/ds-modal@0.0.2")]
    [InlineData("pkg:npm/monorepolint@0.4.0")]
    [InlineData("pkg:npm/example@0.0.0")]
    [InlineData("pkg:npm/rly-cli@0.0.2")]
    [InlineData("pkg:npm/lodash.js@0.0.1-security")]
    [InlineData("pkg:npm/tslib@2.4.1")]
    public async Task PackageVersionExistsAsyncSucceeds(string purlString)
    {
        PackageURL purl = new(purlString);

        Assert.True(await _projectManager.Object.PackageVersionExistsAsync(purl, useCache: false));
    }

    [Theory]
    [InlineData("pkg:npm/lodash@1.2.3.4.5.6.7")]
    [InlineData("pkg:npm/%40angular/core@1.2.3.4.5.6.7")]
    [InlineData("pkg:npm/%40achievementify/client@0.2.1")]
    public async Task PackageVersionDoesntExistsAsyncSucceeds(string purlString)
    {
        PackageURL purl = new(purlString);

        Assert.False(await _projectManager.Object.PackageVersionExistsAsync(purl, useCache: false));
    }
    
    [Theory]
    [InlineData("pkg:npm/tslib@2.4.1", "currently_exists")]
    public async Task DetailedPackageVersionExistsAsync_PurlSucceeds(string purlString, string existenceKey)
    {
        PackageURL purl = new(purlString);

        var existence = await _projectManager.Object.DetailedPackageVersionExistsAsync(purl, useCache: false);
        existence.Should().BeEquivalentTo(_packageVersionExistence[existenceKey].packageVersionExistence);
    }

    [Theory]
    [InlineData("currently_exists")] // The package version currently exists
    [InlineData("version_pulled")] // The package version was pulled from the registry
    [InlineData("version_never_existed")] // The package version never existed
    [InlineData("package_considered_malicious")] // The package was removed for security
    public async Task DetailedPackageVersionExistsAsyncSucceeds(
        string key)
    {
        IPackageExistence expectedPackageVersionExistence = _packageVersionExistence[key].packageVersionExistence;
        bool versionPulled = _packageVersionExistence[key].versionPulled;
        bool consideredMalicious = _packageVersionExistence[key].consideredMalicious;
        
        if (!expectedPackageVersionExistence.Exists)
        {
            _projectManager
                .Setup(p => 
                    p.GetMetadataAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(null as string);
        }

        _projectManager
            .Setup(p => 
                p.PackageVersionExistsAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
            .ReturnsAsync(expectedPackageVersionExistence.Exists);
        _projectManager
            .Setup(p => 
                p.PackageVersionPulled(It.IsAny<PackageURL>(), It.IsAny<JsonElement>()))
            .Returns(versionPulled);

        _projectManager
            .Setup(p => 
                p.PackageConsideredMalicious(It.IsAny<JsonElement>()))
            .Returns(consideredMalicious);

        IPackageExistence packageVersionExistenceResponse =
            await _projectManager.Object.DetailedPackageVersionExistsAsync(new PackageURL("pkg:npm/example@0.0.0"),
                useCache: false);

        packageVersionExistenceResponse.Should().BeEquivalentTo(expectedPackageVersionExistence);
    }
    
    [Theory]
    [InlineData("currently_exists")] // The package currently exists
    [InlineData("never_existed")] // The package never existed
    [InlineData("pulled")] // The package existed but was removed
    [InlineData("considered_malicious")] // The package was removed for security reasons
    public async Task DetailedPackageExistsAsyncSucceeds(
        string key)
    {
        IPackageExistence expectedPackageExistence = _packageExistence[key].packageExistence;
        bool pulled = _packageExistence[key].pulled;
        bool consideredMalicious = _packageExistence[key].consideredMalicious;

        if (!expectedPackageExistence.Exists)
        {
            _projectManager
                .Setup(p => 
                    p.GetMetadataAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
                .ReturnsAsync(null as string);
        }

        _projectManager
            .Setup(p => 
                p.PackageExistsAsync(It.IsAny<PackageURL>(), It.IsAny<bool>()))
            .ReturnsAsync(expectedPackageExistence.Exists);
        _projectManager
            .Setup(p => 
                p.PackagePulled(It.IsAny<JsonElement>()))
            .Returns(pulled);
        _projectManager
            .Setup(p => 
                p.PackageConsideredMalicious(It.IsAny<JsonElement>()))
            .Returns(consideredMalicious);

        IPackageExistence packageExistenceResponse =
            await _projectManager.Object.DetailedPackageExistsAsync(new PackageURL("pkg:npm/example@0.0.0"),
                useCache: false);

        packageExistenceResponse.Should().BeEquivalentTo(expectedPackageExistence);
    }
    
    [Theory]
    [InlineData("pkg:npm/%40somosme/webflowutils@1.0.0")]
    [InlineData("pkg:npm/%40somosme/webflowutils@1.2.3", false)]
    [InlineData("pkg:npm/%40achievementify/client@0.2.1")]
    [InlineData("pkg:npm/%40achievementify/client@0.2.3", false)]
    public async Task PackageVersionPulledAsync(string purlString, bool expectedPulled = true)
    {
        PackageURL purl = new(purlString);

        var project = _projectManager;
        
        string? content = await _projectManager.Object.GetMetadataAsync(purl);

        JsonDocument contentJSON = JsonDocument.Parse(content);
        JsonElement root = contentJSON.RootElement;

        Assert.Equal(expectedPulled,  _projectManager.Object.PackageVersionPulled(purl, root));
    }
    
    [Theory]
    [InlineData("pkg:npm/lodash.js")]
    [InlineData("pkg:npm/lodash.js@1.0.0")]
    [InlineData("pkg:npm/lodash", false)]
    public async Task PackageSecurityHoldingAsync(string purlString, bool expectedToHaveSecurityHolding = true)
    {
        PackageURL purl = new(purlString);

        string? content = await _projectManager.Object.GetMetadataAsync(purl);

        JsonDocument contentJSON = JsonDocument.Parse(content);
        JsonElement root = contentJSON.RootElement;

        Assert.Equal(expectedToHaveSecurityHolding, _projectManager.Object.PackageConsideredMalicious(root));
    }
    
    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15", "2019-07-19T02:28:46.584")]
    [InlineData("pkg:npm/%40angular/core@13.2.5", "2022-03-02T18:25:31.169")]
    [InlineData("pkg:npm/ds-modal@0.0.2", "2018-08-09T07:24:06.206")]
    [InlineData("pkg:npm/monorepolint@0.4.0", "2019-08-07T16:20:53.525")]
    [InlineData("pkg:npm/rly-cli@0.0.2", "2022-03-08T17:26:27.219")]
    [InlineData("pkg:npm/example@0.0.0", "2022-08-10T21:35:38.278")]
    [InlineData("pkg:npm/example@0.0.1")] // No time property in the json for this version
    public async Task GetPublishedAtSucceeds(string purlString, string? expectedTime = null)
    {
        PackageURL purl = new(purlString);
        DateTime? time = await _projectManager.Object.GetPublishedAtUtcAsync(purl, useCache: false);

        if (expectedTime == null)
        {
            Assert.Null(time);
        }
        else
        {
            Assert.Equal(DateTime.Parse(expectedTime), time);
        }
    }

    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15", "2012-04-23T16:37:11.912")]
    [InlineData("pkg:npm/%40angular/core@13.2.5", "2016-04-28T04:23:30.108")]
    [InlineData("pkg:npm/ds-modal@0.0.2", "2018-08-06T12:04:34.792")]
    [InlineData("pkg:npm/monorepolint@0.4.0", "2018-12-19T23:29:18.197")]
    [InlineData("pkg:npm/rly-cli@0.0.2", "2022-03-04T05:57:01.108")]
    public async Task GetCreatedAtSucceeds(string purlString, string? expectedTime = null)
    {
        PackageURL purl = new(purlString);
        var metadata = await _projectManager.Object.GetPackageMetadataAsync(purl, useCache: false);
        Assert.Equal(DateTime.Parse(expectedTime), metadata.CreatedTime);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FetchesRepositoryMetadataSuccessfully(bool includeRepositoryMetadata)
    {
        PackageURL purl = new("pkg:npm/lodash.js");
        var metadata = await _projectManager.Object.GetPackageMetadataAsync(purl, includeRepositoryMetadata: includeRepositoryMetadata);
       
        if(includeRepositoryMetadata)
        {
            Assert.NotNull(metadata.Repository);
        }
        else
        {
            Assert.Null(metadata.Repository);
        }
    }

    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15", "https://registry.npmjs.org/lodash/-/lodash-4.17.15.tgz")]
    [InlineData("pkg:npm/%40angular/core@13.2.5", "https://registry.npmjs.org/%40angular/core/-/core-13.2.5.tgz")]
    [InlineData("pkg:npm/ds-modal@0.0.2", "https://registry.npmjs.org/ds-modal/-/ds-modal-0.0.2.tgz")]
    [InlineData("pkg:npm/monorepolint@0.4.0", "https://registry.npmjs.org/monorepolint/-/monorepolint-0.4.0.tgz")]
    [InlineData("pkg:npm/example@0.0.0", "https://registry.npmjs.org/example/-/example-0.0.0.tgz")]
    [InlineData("pkg:npm/rly-cli@0.0.2", "https://registry.npmjs.org/rly-cli/-/rly-cli-0.0.2.tgz")]
    public async Task GetArtifactDownloadUrisSucceeds_Async(string purlString, string expectedUri)
    {
        PackageURL purl = new(purlString);
        List<ArtifactUri<NPMProjectManager.NPMArtifactType>> uris = _projectManager.Object.GetArtifactDownloadUris(purl).ToList();

        Assert.Equal(expectedUri, uris.First().Uri.AbsoluteUri);
        Assert.Equal(".tgz", uris.First().Extension);
        Assert.Equal(NPMProjectManager.NPMArtifactType.Tarball, uris.First().Type);
        Assert.True(await _projectManager.Object.UriExistsAsync(uris.First().Uri));
    }
    
    [Theory]
    [InlineData("jdalton", "pkg:npm/lodash")]
    [InlineData("microsoft", "pkg:npm/%40microsoft/rush")]
    [InlineData("azure", "pkg:npm/%40azure/cosmos")]
    [InlineData("azure", "pkg:npm/%40azure/graph")]
    public async Task GetPackagesFromOwnerAsyncSucceeds_Async(string owner, string expectedPackage)
    {
        NPMProjectManager projectManager = new(".");

        List<PackageURL> packages = await _projectManager.Object.GetPackagesFromOwnerAsync(owner).ToListAsync();

        packages.Should().OnlyHaveUniqueItems();
        packages.Select(p => p.ToString()).Should().Contain(expectedPackage);
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
        httpMock.When(HttpMethod.Get, $"{url}/*.tgz").Respond(statusCode);

    }
}
