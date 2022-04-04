// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Model.PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    public class ProjectManagerFactory
    {
        /// <summary>
        /// The delegate to use when defining the function to create a project manager with.
        /// The only runtime parameter we need is the destination directory. Everything else can be defined in the constructor.
        /// </summary>
        public delegate BaseProjectManager? ConstructProjectManager(string destinationDirectory = ".");

        /// <summary>
        /// The dictionary of project managers.
        /// </summary>
        private readonly Dictionary<string, ConstructProjectManager> _projectManagers;

        /// <summary>
        /// Initializes a new instance of a <see cref="ProjectManagerFactory"/>.
        /// </summary>
        /// <param name="projectManagersIn">Use a custom set of managers</param>
        public ProjectManagerFactory(Dictionary<string, ConstructProjectManager> projectManagersIn)
        {
            _projectManagers = projectManagersIn;
        }

        /// <summary>
        /// Construct a ProjectManagerFactory with Default Managers and optionally a specific <see cref="IHttpClientFactory"/>.
        /// If <paramref name="httpClientFactory"/> is null, <see cref="DefaultHttpClientFactory"/> will be used.
        /// </summary>
        /// <param name="httpClientFactory">Optionally, use a provided <see cref="IHttpClientFactory"/> when needed</param>
        public ProjectManagerFactory(IHttpClientFactory? httpClientFactory = null)
        {
            _projectManagers = CreateDefaultManagers(httpClientFactory);
        }

        /// <summary>
        /// Unset any <see cref="ConstructProjectManager"/> for a given <paramref name="lookup"/>
        /// </summary>
        /// <param name="lookup">The PackageUrl Type</param>
        /// <returns>If the <paramref name="lookup"/> was found and removed</returns>
        public bool UnsetManager(string lookup)
        {
            return _projectManagers.Remove(lookup);
        }

        /// <summary>
        /// Set a specific <see cref="ConstructProjectManager"/> for a given <see cref="PackageURL.Type"/>
        /// </summary>
        /// <param name="lookup">The PackageUrl Type to use the provided <paramref name="generator"/> for</param>
        /// <param name="generator">The <see cref="ConstructProjectManager"/> delegate which generates the appropriate Manager.</param>
        public void SetManager(string lookup, ConstructProjectManager generator)
        {
            _projectManagers[lookup] = generator;
        }

        /// <summary>
        /// Unset all manager creators.
        /// </summary>
        public void ClearManagers()
        {
            _projectManagers.Clear();
        }

        /// <summary>
        /// Create the default set of managers, optionally with a specified <see cref="IHttpClientFactory"/>. Otherwise <see cref="DefaultHttpClientFactory"/> will be used when needed.
        /// </summary>
        /// <param name="httpClientFactoryParam"></param>
        /// <returns>A Dictionary with the mapping between <see cref="PackageURL.Type"/> and a <see cref="CreateProjectManager"/> delegate to create the appropriate <see cref="BaseProjectManager"/></returns>
        private Dictionary<string, ConstructProjectManager> CreateDefaultManagers(IHttpClientFactory? httpClientFactoryParam = null)
        {
            // If the httpClientFactory parameter is null, set the factory to the DefaultHttpClientFactory.
            IHttpClientFactory httpClientFactory = httpClientFactoryParam ?? new DefaultHttpClientFactory();
            return new Dictionary<string, ConstructProjectManager>(StringComparer.InvariantCultureIgnoreCase)
            {
                {
                    "Cargo", destinationDirectory =>
                        new CargoProjectManager(destinationDirectory, new CargoPackageActions(httpClientFactory))
                },
                {
                    "Cocoapods", destinationDirectory =>
                        new CocoapodsProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "Composer", destinationDirectory =>
                        new ComposerProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "CPAN", destinationDirectory =>
                        new CPANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "CRAN", destinationDirectory =>
                        new CRANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "Gem", destinationDirectory =>
                        new GemProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "GitHub", destinationDirectory =>
                        new GitHubProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "Golang", destinationDirectory =>
                        new GolangProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "Hackage", destinationDirectory =>
                        new HackageProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "Maven", destinationDirectory =>
                        new MavenProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "NPM", destinationDirectory =>
                        new NPMProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "NuGet", destinationDirectory =>
                        new NuGetProjectManager(destinationDirectory, new NuGetPackageActions(), httpClientFactory) // Add the NuGetPackageActions to the NuGetProjectManager.
                },
                {
                    "PyPI", destinationDirectory =>
                        new PyPIProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "Ubuntu", destinationDirectory =>
                        new UbuntuProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "URL", destinationDirectory =>
                        new URLProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    "VSM", destinationDirectory =>
                        new VSMProjectManager(httpClientFactory, destinationDirectory)
                },
            };
        }
        /// <summary>
        /// Creates an appropriate project manager for a package given its PackageURL.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> for the package to create the project manager for.</param>
        /// <param name="destinationDirectory">The new destination directory, if provided.</param>
        /// <returns>The implementation of <see cref="BaseProjectManager"/> for this <paramref name="purl"/>'s type.</returns>
        public BaseProjectManager? CreateProjectManager(PackageURL purl, string destinationDirectory = ".")
        {
            return _projectManagers.GetValueOrDefault(purl.Type)?.Invoke(destinationDirectory);
        }

        /// <summary>
        /// Add static method to just get a <see cref="BaseProjectManager"/> implementation.
        /// </summary>
        /// <param name="packageUrl">The <see cref="PackageURL"/> to get a project manager for.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to optionally add.</param>
        /// <param name="nugetPackageActions">The <see cref="NuGetPackageActions"/> to use in the <see cref="NuGetProjectManager"/>.</param>
        /// <returns>A new <see cref="BaseProjectManager"/> implementation.</returns>
        public static BaseProjectManager? GetProjectManager(PackageURL packageUrl, IHttpClientFactory? httpClientFactory = null)
        {
            return new ProjectManagerFactory(httpClientFactory).CreateProjectManager(packageUrl);
        }
    }
}