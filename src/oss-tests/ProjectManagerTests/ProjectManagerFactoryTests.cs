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

    private readonly List<Dictionary<string, ProjectManagerFactory.ConstructProjectManager>> _overrideConstructors;
    
    public ProjectManagerFactoryTests()
    {
        MockHttpMessageHandler mockHttp = new();

        _mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttp.ToHttpClient());
        
        _overrideConstructors = new List<Dictionary<string, ProjectManagerFactory.ConstructProjectManager>>
        {
            new()
            {
                {"test", directory => new NuGetProjectManager(_mockFactory.Object, directory) } // Create a test type with the NuGetProjectManager.
            },
            new()
            {
                {"nuget", directory => new NuGetProjectManager(_mockFactory.Object, directory) } // Create a new nuget type and override the NuGetProjectManager.
            },
            new()
            {
                {"nuget", directory => new NPMProjectManager(_mockFactory.Object, directory) } // Create a new nuget type, but use an NPMProjectManager instead.
            },
            new()
            {
                {"nuget", directory => new NuGetProjectManager(_mockFactory.Object, directory) }, // Create a new nuget type and override the NuGetProjectManager.
                {"npm", directory => new NPMProjectManager(_mockFactory.Object, directory) }, // Create a new npm type and override the NPMProjectManager.
                {"pypi", directory => new PyPIProjectManager(_mockFactory.Object, directory) } // Create a new pypi type and override the PyPIProjectManager.
            },
        };
    }

    [DataTestMethod]
    [DataRow("pkg:nuget/newtonsoft.json", typeof(NuGetProjectManager))]
    [DataRow("pkg:npm/foo", typeof(NPMProjectManager))]
    [DataRow("pkg:pypi/plotly", typeof(PyPIProjectManager))]
    public void FactorySucceeds(string purlString, Type expectedManager)
    {
        ProjectManagerFactory projectManagerFactory = new(_mockFactory.Object);

        PackageURL packageUrl = new(purlString);

        Assert.AreEqual(expectedManager, projectManagerFactory.CreateProjectManager(packageUrl)?.GetType());
    }
    
    [DataTestMethod]
    [DataRow(null)] // Add nothing, remove nothing.
    [DataRow(0)] // Add a "test" type with the NuGetProjectManager, remove nothing.
    [DataRow(1)] // Add a "nuget" type with the NuGetProjectManager, overriding the default, remove nothing.
    [DataRow(2)] // Add a "nuget" type but use an NPMProjectManager, overriding the default, remove nothing.
    [DataRow(3)] // Add "nuget", "npm" and "pypi" types, overriding the defaults, remove nothing.
    [DataRow(null, "nuget")] // Add nothing, remove "nuget".
    [DataRow(null, "nuget", "npm")] // Add nothing, remove "nuget" & "npm".
    public void FactoryOverrideDefaultsSucceeds(int? overrideSelect = null, params string[] removeTypes)
    {
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> addOverrides = new();
        if (overrideSelect is not null)
        {
            addOverrides = _overrideConstructors[(int) overrideSelect];
        }

        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides = ProjectManagerFactory.GetDefaultManagers(_mockFactory.Object)
            .Where(kvp => !removeTypes.Contains(kvp.Key) && !addOverrides.ContainsKey(kvp.Key))
            .Concat(addOverrides).ToDictionary(x => x.Key, x => x.Value);
        
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);

        foreach ((string purlType, ProjectManagerFactory.ConstructProjectManager ctor) in addOverrides)
        {
            PackageURL packageUrl = new($"pkg:{purlType}/foo");
            BaseProjectManager? expectedManager = ctor.Invoke();
            BaseProjectManager? manager = projectManagerFactory.CreateProjectManager(packageUrl);
            Assert.AreEqual(expectedManager?.ManagerType, manager?.ManagerType);
            Assert.AreEqual(expectedManager?.GetType(), manager?.GetType());
        }
        
        foreach ((string purlType, ProjectManagerFactory.ConstructProjectManager ctor) in managerOverrides)
        {
            PackageURL packageUrl = new($"pkg:{purlType}/foo");
            BaseProjectManager? expectedManager = ctor.Invoke();
            BaseProjectManager? manager = projectManagerFactory.CreateProjectManager(packageUrl);
            Assert.AreEqual(expectedManager?.ManagerType, manager?.ManagerType);
            Assert.AreEqual(expectedManager?.GetType(), manager?.GetType());
        }

        if (removeTypes.Any())
        {
            foreach (string purlType in removeTypes)
            {
                PackageURL packageUrl = new($"pkg:{purlType}/foo");
                Assert.IsNull(projectManagerFactory.CreateProjectManager(packageUrl));
            }
        }
    }
}