// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests;

using FluentAssertions;
using Microsoft.CST.OpenSource;
using Microsoft.CST.OpenSource.Contracts;
using Microsoft.CST.OpenSource.PackageActions;
using Microsoft.CST.OpenSource.PackageManagers;
using Moq;
using PackageUrl;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public class ProjectManagerFactoryTests
{
    private readonly IHttpClientFactory _httpClientFactory = new DefaultHttpClientFactory();
    private readonly Dictionary<string, ProjectManagerFactory.ConstructProjectManager> _managerOverrides;

    public ProjectManagerFactoryTests()
    {
        // Set up the manager overrides as the default for now.
        _managerOverrides = ProjectManagerFactory.GetDefaultManagers(_httpClientFactory);
    }

    /// <summary>
    /// Test that the <see cref="ProjectManagerFactory"/> creates the correct ProjectManager for the <paramref name="purlString"/>.
    /// </summary>
    /// <param name="purlString">The <see cref="PackageURL"/> as a string to create a manager for from the factory</param>
    /// <param name="expectedManager">The <see cref="Type"/> of the <see cref="BaseProjectManager"/> implementation we expect the factory to return.</param>
    [Theory]
    [InlineData("pkg:nuget/newtonsoft.json", typeof(NuGetProjectManager))]
    [InlineData("pkg:nuget/NuGet@1.3.3?repository_url=https://www.powershellgallery.com/api/v2", typeof(NuGetProjectManagerV2))]
    [InlineData("pkg:npm/foo", typeof(NPMProjectManager))]
    [InlineData("pkg:pypi/plotly", typeof(PyPIProjectManager))]
    [InlineData("pkg:cargo/rand", typeof(CargoProjectManager))]
    [InlineData("pkg:foo/bar", null)]
    public void FactorySucceeds(string purlString, Type? expectedManager)
    {
        ProjectManagerFactory projectManagerFactory = new(_httpClientFactory);

        PackageURL packageUrl = new(purlString);

        Assert.Equal(expectedManager, projectManagerFactory.CreateProjectManager(packageUrl)?.GetType());
    }

    [Fact]
    public void CargoPackageManagerCreatedWithDisablingRateLimitedRegistryAPI()
    {
        var purlString = "pkg:cargo/rand";
        ProjectManagerFactory projectManagerFactory = new(allowUseOfRateLimitedRegistryAPIs:false);

        PackageURL packageUrl = new(purlString);
        var manager = projectManagerFactory.CreateProjectManager(packageUrl);

        Assert.Equal(typeof(CargoProjectManager), manager.GetType());
        Assert.False(((CargoProjectManager)manager).allowUseOfRateLimitedRegistryAPIs);
    }


    /// <summary>
    /// Test if the default dictionary works, similar to <see cref="FactorySucceeds"/>, but uses the override constructor.
    /// </summary>
    [Fact]
    public void DefaultSucceeds()
    {
        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);
    }

    /// <summary>
    /// Test adding a new manager to the dictionary, not overriding an existing one.
    /// </summary>
    [Fact]
    public void AddTestManagerSucceeds()
    {
        _managerOverrides["test"] = (destinationDirectory, timeout, packageUrl) => new NuGetProjectManagerV3(destinationDirectory, null, _httpClientFactory); // Create a test type with the NuGetProjectManager.

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);
    }

    /// <summary>
    /// Test overriding the nuget and npm entries as well as their destination directories in the dictionary.
    /// </summary>
    [Fact]
    public void OverrideManagerSucceeds()
    {
        _managerOverrides[NuGetProjectManager.Type] =
            (destinationDirectory, timeout, packageUrl) => new NuGetProjectManagerV3("nugetTestDirectory", null, _httpClientFactory); // Override the default entry for nuget, and override the destinationDirectory.
        _managerOverrides[NPMProjectManager.Type] =
            (destinationDirectory, timeout, packageUrl) => new NPMProjectManager("npmTestDirectory", null, _httpClientFactory); // Override the default entry for npm, and override the destinationDirectory.

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);

        // Assert that the overrides worked by checking the TopLevelExtractionDirectory was changed.
        IBaseProjectManager? nuGetProjectManager = projectManagerFactory.CreateProjectManager(new PackageURL("pkg:nuget/foo"));
        Assert.Equal("nugetTestDirectory", nuGetProjectManager?.TopLevelExtractionDirectory);

        IBaseProjectManager? npmProjectManager = projectManagerFactory.CreateProjectManager(new PackageURL("pkg:npm/foo"));
        Assert.Equal("npmTestDirectory", npmProjectManager?.TopLevelExtractionDirectory);
    }

    /// <summary>
    /// Test changing an entry in the dictionary of constructors to construct a manager of a different type.
    /// </summary>
    [Fact]
    public void ChangeProjectManagerSucceeds()
    {
        _managerOverrides[NuGetProjectManager.Type] = (destinationDirectory, timeout, packageUrl) => new NPMProjectManager(destinationDirectory, null, _httpClientFactory); // Override the default entry for nuget and set it as another NPMProjectManager.

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);
    }

    /// <summary>
    /// Test removing an entry from the default dictionary of project manager constructors.
    /// </summary>
    [Fact]
    public void RemoveProjectManagerSucceeds()
    {
        Assert.True(_managerOverrides.Remove(NuGetProjectManager.Type));

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        PackageURL packageUrl = new("pkg:nuget/foo");
        Assert.Null(projectManagerFactory.CreateProjectManager(packageUrl));
    }

    /// <summary>
    /// Test removing all project managers from the dictionary of project manager constructors.
    /// </summary>
    /// <remarks>The <see cref="ProjectManagerFactory"/> should only ever return null in this case.</remarks>
    [Fact]
    public void RemoveAllProjectManagersSucceeds()
    {
        _managerOverrides.Clear();

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);

        foreach (PackageURL packageUrl in ProjectManagerFactory.GetDefaultManagers(_httpClientFactory).Keys
                     .Select(purlType => new PackageURL($"pkg:{purlType}/foo")))
        {
            Assert.Null(projectManagerFactory.CreateProjectManager(packageUrl));
        }
    }

    /// <summary>
    /// Test that timeout is set if a value is passed.
    /// </summary>
    [Fact]
    public void CreateProjectManagerSetsTimeOutCorrectly()
    {
        // Arrange
        ProjectManagerFactory projectManagerFactory = new();
        TimeSpan testTimeout = TimeSpan.FromMilliseconds(100);
        PackageURL testPackageUrl = new("pkg:npm/foo");

        // Act
        IBaseProjectManager? testProjectManager = projectManagerFactory.CreateProjectManager(testPackageUrl, ".", testTimeout);
        IBaseProjectManager? testProjectManagerWithoutTimeout = projectManagerFactory.CreateProjectManager(testPackageUrl, ".");

        // Assert
        Assert.Equal(testTimeout, testProjectManager?.Timeout);
        Assert.Null(testProjectManagerWithoutTimeout?.Timeout);
    }

    /// <summary>
    /// Test that requests time out after the timespan if specified.
    /// </summary>
    [Fact]
    public async Task PackageManagerRequestsTimeOutCorrectly()
    {
        // Arrange
        Mock<IHttpClientFactory> mockFactory = new();
        MockHttpMessageHandler mockHttp = new();
        mockHttp
            .When(HttpMethod.Get, "*")
            .Respond(async () =>
            {
                await Task.Delay(120); // simulate a delay slightly longer than the timeout set
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("This is a delayed response")
                };
            });
        HttpClient testClient = mockHttp.ToHttpClient();
        testClient.Timeout = TimeSpan.FromMilliseconds(100);
        mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(testClient);
        IHttpClientFactory httpFactory = mockFactory.Object;
        PackageURL testPackageUrl = new("pkg:npm/foo@0.1");

        Mock<NPMProjectManager> testProjectManager = new Mock<NPMProjectManager>(".", new NoOpPackageActions(), httpFactory, null) { CallBase = true };

        //Act
        // NOTE that the usual FluentAssertion throw exception does not work with the above setup.
        TaskCanceledException? ex = null;
        try
        {
            await testProjectManager.Object.DownloadVersionAsync(testPackageUrl, false, false);
        }
        catch(TaskCanceledException tex)
        {
            ex = tex;
        }
        // Assert
        ex.Should().NotBeNull();
        ex.InnerException.Should().NotBeNull();
        Type innerExceptionType = ex.InnerException.GetType();
        innerExceptionType.Should().Be(typeof(TimeoutException));
    }

    /// <summary>
    /// Helper method to assert that the <paramref name="projectManagerFactory"/> creates the expected implementation of <see cref="BaseProjectManager"/>.
    /// </summary>
    /// <param name="projectManagerFactory">The <see cref="ProjectManagerFactory"/> to use.</param>
    private void AssertFactoryCreatesCorrect(ProjectManagerFactory projectManagerFactory)
    {
        foreach ((string purlType, ProjectManagerFactory.ConstructProjectManager ctor) in _managerOverrides)
        {
            PackageURL packageUrl = new($"pkg:{purlType}/foo");
            IBaseProjectManager? expectedManager = ctor.Invoke();
            IBaseProjectManager? manager = projectManagerFactory.CreateProjectManager(packageUrl);
            Assert.Equal(expectedManager?.ManagerType, manager?.ManagerType);
            Assert.Equal(expectedManager?.GetType(), manager?.GetType());
        }
    }
}