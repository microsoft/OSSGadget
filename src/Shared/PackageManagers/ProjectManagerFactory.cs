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

        public ProjectManagerFactory(IHttpClientFactory? httpClientFactory = null)
        {
            _projectManagers = CreateDefaultManagers(httpClientFactory);
        }

        public bool UnsetManager(string lookup)
        {
            return _projectManagers.Remove(lookup);
        }

        public void SetManager(string lookup, ConstructProjectManager generator)
        {
            _projectManagers[lookup] = generator;
        }

        public void ClearManagers()
        {
            _projectManagers.Clear();
        }

        public Dictionary<string, ConstructProjectManager> CreateDefaultManagers(IHttpClientFactory? httpClientFactoryParam = null)
        {
            // If the httpClientFactory parameter is null, set the factory to the DefaultHttpClientFactory.
            IHttpClientFactory httpClientFactory = httpClientFactoryParam ?? new DefaultHttpClientFactory();
            return new Dictionary<string, ConstructProjectManager>(StringComparer.InvariantCultureIgnoreCase)
            {
                {
                    nameof(CargoProjectManager), destinationDirectory =>
                        new CargoProjectManager(destinationDirectory, new CargoPackageActions(httpClientFactory))
                },
                {
                    nameof(CocoapodsProjectManager), destinationDirectory =>
                        new CocoapodsProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(ComposerProjectManager), destinationDirectory =>
                        new ComposerProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(CPANProjectManager), destinationDirectory =>
                        new CPANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(CRANProjectManager), destinationDirectory =>
                        new CRANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(GemProjectManager), destinationDirectory =>
                        new GemProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(GitHubProjectManager), destinationDirectory =>
                        new GitHubProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(GolangProjectManager), destinationDirectory =>
                        new GolangProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(HackageProjectManager), destinationDirectory =>
                        new HackageProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(MavenProjectManager), destinationDirectory =>
                        new MavenProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(NPMProjectManager), destinationDirectory =>
                        new NPMProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(NuGetProjectManager), destinationDirectory =>
                        new NuGetProjectManager(destinationDirectory, new NuGetPackageActions(), httpClientFactory) // Add the NuGetPackageActions to the NuGetProjectManager.
                },
                {
                    nameof(PyPIProjectManager), destinationDirectory =>
                        new PyPIProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(UbuntuProjectManager), destinationDirectory =>
                        new UbuntuProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(URLProjectManager), destinationDirectory =>
                        new URLProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(VSMProjectManager), destinationDirectory =>
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
            // TODO: This should do lookups based directly on the purl.Type - not add ProjectManager to it.
            ConstructProjectManager? projectManager = _projectManagers.GetValueOrDefault($"{purl.Type}ProjectManager");

            return projectManager?.Invoke(destinationDirectory);
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