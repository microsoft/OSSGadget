// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests.ProjectManagerTests;

using Contracts;
using PackageActions;
using PackageManagers;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using VisualStudio.TestTools.UnitTesting;

[TestClass]
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
    [DataTestMethod]
    [DataRow("pkg:nuget/newtonsoft.json", typeof(NuGetProjectManager))]
    [DataRow("pkg:npm/foo", typeof(NPMProjectManager))]
    [DataRow("pkg:pypi/plotly", typeof(PyPIProjectManager))]
    [DataRow("pkg:foo/bar", null)]
    public void FactorySucceeds(string purlString, Type? expectedManager)
    {
        ProjectManagerFactory projectManagerFactory = new(_httpClientFactory);

        PackageURL packageUrl = new(purlString);

        Assert.AreEqual(expectedManager, projectManagerFactory.CreateProjectManager(packageUrl)?.GetType());
    }

    /// <summary>
    /// Test if the default dictionary works, similar to <see cref="FactorySucceeds"/>, but uses the override constructor.
    /// </summary>
    [TestMethod]
    public void DefaultSucceeds()
    {
        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);
    }
    
    /// <summary>
    /// Test adding a new manager to the dictionary, not overriding an existing one.
    /// </summary>
    [TestMethod]
    public void AddTestManagerSucceeds()
    {
        _managerOverrides["test"] = directory => new NuGetProjectManager(directory, null, _httpClientFactory); // Create a test type with the NuGetProjectManager.

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);
    }
    
    /// <summary>
    /// Test overriding the nuget and npm entries as well as their destination directories in the dictionary.
    /// </summary>
    [TestMethod]
    public void OverrideManagerSucceeds()
    {
        _managerOverrides[NuGetProjectManager.Type] =
            _ => new NuGetProjectManager("nugetTestDirectory", null, _httpClientFactory); // Override the default entry for nuget, and override the destinationDirectory.
        _managerOverrides[NPMProjectManager.Type] =
            _ => new NPMProjectManager("npmTestDirectory", null, _httpClientFactory); // Override the default entry for npm, and override the destinationDirectory.

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);

        // Assert that the overrides worked by checking the TopLevelExtractionDirectory was changed.
        IBaseProjectManager? nuGetProjectManager = projectManagerFactory.CreateProjectManager(new PackageURL("pkg:nuget/foo"));
        Assert.AreEqual("nugetTestDirectory", nuGetProjectManager?.TopLevelExtractionDirectory);
        
        IBaseProjectManager? npmProjectManager = projectManagerFactory.CreateProjectManager(new PackageURL("pkg:npm/foo"));
        Assert.AreEqual("npmTestDirectory", npmProjectManager?.TopLevelExtractionDirectory);
    }
    
    /// <summary>
    /// Test changing an entry in the dictionary of constructors to construct a manager of a different type.
    /// </summary>
    [TestMethod]
    public void ChangeProjectManagerSucceeds()
    {
        _managerOverrides[NuGetProjectManager.Type] = directory => new NPMProjectManager(directory, null, _httpClientFactory); // Override the default entry for nuget and set it as another NPMProjectManager.
        
        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);

        AssertFactoryCreatesCorrect(projectManagerFactory);
    }
    
    /// <summary>
    /// Test removing an entry from the default dictionary of project manager constructors.
    /// </summary>
    [TestMethod]
    public void RemoveProjectManagerSucceeds()
    {
        Assert.IsTrue(_managerOverrides.Remove(NuGetProjectManager.Type));

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);
        
        PackageURL packageUrl = new("pkg:nuget/foo");
        Assert.IsNull(projectManagerFactory.CreateProjectManager(packageUrl));
    }
    
    /// <summary>
    /// Test removing all project managers from the dictionary of project manager constructors.
    /// </summary>
    /// <remarks>The <see cref="ProjectManagerFactory"/> should only ever return null in this case.</remarks>
    [TestMethod]
    public void RemoveAllProjectManagersSucceeds()
    {
        _managerOverrides.Clear();

        ProjectManagerFactory projectManagerFactory = new(_managerOverrides);
        
        AssertFactoryCreatesCorrect(projectManagerFactory);
        
        foreach (PackageURL packageUrl in ProjectManagerFactory.GetDefaultManagers(_httpClientFactory).Keys
                     .Select(purlType => new PackageURL($"pkg:{purlType}/foo")))
        {
            Assert.IsNull(projectManagerFactory.CreateProjectManager(packageUrl));
        }
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
            Assert.AreEqual(expectedManager?.ManagerType, manager?.ManagerType);
            Assert.AreEqual(expectedManager?.GetType(), manager?.GetType());
        }
    }
}