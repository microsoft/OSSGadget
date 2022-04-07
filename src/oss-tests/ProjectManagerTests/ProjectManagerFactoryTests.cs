// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests;

using Moq;
using PackageManagers;
using PackageUrl;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ProjectManagerFactoryTests
{
    private readonly Mock<IHttpClientFactory> _mockFactory = new();

    public ProjectManagerFactoryTests()
    {
        MockHttpMessageHandler mockHttp = new();

        _mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
    }

    /// <summary>
    /// Test that the <see cref="ProjectManagerFactory"/> creates the correct ProjectManager for the <paramref name="purlString"/>.
    /// </summary>
    /// <param name="purlString">The <see cref="PackageURL"/> as a string to create a manager for from the factory</param>
    /// <param name="expectedManager">The <see cref="Type"/> of the <see cref="BaseProjectManager"/> implementation we expect the factory to return.</param>
    [DataTestMethod]
    [DataRow("pkg:nuget/newtonsoft.json", typeof(NuGetProjectManager))]
    [DataRow("pkg:npm/foo", typeof(NPMProjectManager))]
    [DataRow("pkg:pypi/plotly", typeof(PyPIProjectManager))]
    [DataRow("pkg:foo/bar", null)]
    public void FactorySucceeds(string purlString, Type? expectedManager)
    {
        ProjectManagerFactory projectManagerFactory = new(_mockFactory.Object);

        PackageURL packageUrl = new(purlString);

        Assert.AreEqual(expectedManager, projectManagerFactory.CreateProjectManager(packageUrl)?.GetType());
    }

    /// <summary>
    /// Test if the default dictionary works, similar to <see cref="FactorySucceeds"/>, but uses the override constructor.
    /// </summary>
    [TestMethod]
    public void DefaultSucceeds()
    {
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides =
            ManagerOverridesConstructor(_mockFactory.Object, null, null);
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory, managerOverrides);
    }
    
    /// <summary>
    /// Test adding a new manager to the dictionary, not overriding an existing one.
    /// </summary>
    [TestMethod]
    public void AddTestManagerSucceeds()
    {
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> addManagers = new()
        {
            {
                "test", directory => new NuGetProjectManager(_mockFactory.Object, directory) // Create a test type with the NuGetProjectManager.
            }
        };

        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides =
            ManagerOverridesConstructor(_mockFactory.Object, addManagers, null);
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);
        
        AssertFactoryCreatesCorrect(projectManagerFactory, addManagers);

        AssertFactoryCreatesCorrect(projectManagerFactory, managerOverrides);
    }
    
    /// <summary>
    /// Test overriding the nuget and npm entries as well as their destination directories in the dictionary.
    /// </summary>
    [TestMethod]
    public void OverrideManagerSucceeds()
    {
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> addManagers = new()
        {
            {
                "nuget", _ => new NuGetProjectManager(_mockFactory.Object, "nugetTestDirectory") // Override the default entry for nuget, and override the destinationDirectory.
            },
            {
                "npm", _ => new NPMProjectManager(_mockFactory.Object, "npmTestDirectory") // Override the default entry for npm, and override the destinationDirectory.
            }
        };

        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides =
            ManagerOverridesConstructor(_mockFactory.Object, addManagers, null);
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);
        
        AssertFactoryCreatesCorrect(projectManagerFactory, addManagers);

        AssertFactoryCreatesCorrect(projectManagerFactory, managerOverrides);

        // Assert that the overrides worked by checking the TopLevelExtractionDirectory was changed.
        BaseProjectManager? nuGetProjectManager = projectManagerFactory.CreateProjectManager(new PackageURL("pkg:nuget/foo"));
        Assert.AreEqual("nugetTestDirectory", nuGetProjectManager?.TopLevelExtractionDirectory);
        
        BaseProjectManager? npmProjectManager = projectManagerFactory.CreateProjectManager(new PackageURL("pkg:npm/foo"));
        Assert.AreEqual("npmTestDirectory", npmProjectManager?.TopLevelExtractionDirectory);
    }
    
    /// <summary>
    /// Test changing an entry in the dictionary of constructors to construct a manager of a different type.
    /// </summary>
    [TestMethod]
    public void ChangeProjectManagerSucceeds()
    {
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> addManagers = new()
        {
            {
                "nuget", directory => new NPMProjectManager(_mockFactory.Object, directory) // Override the default entry for nuget and set it as another NPMProjectManager.
            }
        };

        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides =
            ManagerOverridesConstructor(_mockFactory.Object, addManagers, null);
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);
        
        AssertFactoryCreatesCorrect(projectManagerFactory, addManagers);

        AssertFactoryCreatesCorrect(projectManagerFactory, managerOverrides);
    }
    
    /// <summary>
    /// Test removing an entry from the default dictionary of project manager constructors.
    /// </summary>
    [TestMethod]
    public void RemoveProjectManagerSucceeds()
    {
        string[] removeTypes = { "nuget" };

        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides =
            ManagerOverridesConstructor(_mockFactory.Object, null, removeTypes);
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);
        
        AssertFactoryCreatesCorrect(projectManagerFactory, managerOverrides);
        
        foreach (string purlType in removeTypes)
        {
            PackageURL packageUrl = new($"pkg:{purlType}/foo");
            Assert.IsNull(projectManagerFactory.CreateProjectManager(packageUrl));
        }
    }
    
    /// <summary>
    /// Test removing all project managers from the dictionary of project manager constructors.
    /// </summary>
    /// <remarks>The <see cref="ProjectManagerFactory"/> should only ever return null in this case.</remarks>
    [TestMethod]
    public void RemoveAllProjectManagersSucceeds()
    {
        string[] removeTypes = ProjectManagerFactory.GetDefaultManagers(_mockFactory.Object).Select(kvp => kvp.Key).ToArray();

        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides =
            ManagerOverridesConstructor(_mockFactory.Object, null, removeTypes);
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);
        
        AssertFactoryCreatesCorrect(projectManagerFactory, managerOverrides);
        
        foreach (string purlType in removeTypes)
        {
            PackageURL packageUrl = new($"pkg:{purlType}/foo");
            Assert.IsNull(projectManagerFactory.CreateProjectManager(packageUrl));
        }
    }

    /// <summary>
    /// Helper method to create the modified dictionary of constructors.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use in the constructors.</param>
    /// <param name="addManagers">The <see cref="Dictionary{String,ConstructProjectManager}"/> of managers to override the constructors for in the factory.</param>
    /// <param name="removeTypes">The <see cref="PackageURL.Type"/>s to remove from being able to be constructed by the factory.</param>
    /// <returns>The new <see cref="Dictionary{String,ConstructProjectManager}"/> that the <see cref="ProjectManagerFactory"/> should use.</returns>
    private static Dictionary<string, ProjectManagerFactory.ConstructProjectManager> ManagerOverridesConstructor(
        IHttpClientFactory httpClientFactory,
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager>? addManagers = null,
        string[]? removeTypes = null)
    {
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> addOverrides =
            addManagers ?? new Dictionary<string, ProjectManagerFactory.ConstructProjectManager>();

        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides = ProjectManagerFactory.GetDefaultManagers(httpClientFactory)
            .Where(kvp => (removeTypes != null && !removeTypes.Contains(kvp.Key)) && !addOverrides.ContainsKey(kvp.Key))
            .Concat(addOverrides).ToDictionary(x => x.Key, x => x.Value);

        return managerOverrides;
    }

    /// <summary>
    /// Helper method to assert that the <paramref name="projectManagerFactory"/> creates the expected implementation of <see cref="BaseProjectManager"/>.
    /// </summary>
    /// <param name="projectManagerFactory">The <see cref="ProjectManagerFactory"/> to use.</param>
    /// <param name="dict">The <see cref="Dictionary{String,ConstructProjectManager}"/> to loop through to test on the <paramref name="projectManagerFactory"/>.</param>
    private static void AssertFactoryCreatesCorrect(ProjectManagerFactory projectManagerFactory, Dictionary<string, ProjectManagerFactory.ConstructProjectManager> dict)
    {
        foreach ((string purlType, ProjectManagerFactory.ConstructProjectManager ctor) in dict)
        {
            PackageURL packageUrl = new($"pkg:{purlType}/foo");
            BaseProjectManager? expectedManager = ctor.Invoke();
            BaseProjectManager? manager = projectManagerFactory.CreateProjectManager(packageUrl);
            Assert.AreEqual(expectedManager?.ManagerType, manager?.ManagerType);
            Assert.AreEqual(expectedManager?.GetType(), manager?.GetType());
        }
    }
}