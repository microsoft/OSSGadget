// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        /// <param name="overrideManagers">Use a custom set of managers to override the defaults with.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use in the project managers.</param>
        public ProjectManagerFactory(Dictionary<string, ConstructProjectManager> overrideManagers, IHttpClientFactory? httpClientFactory = null) : this(httpClientFactory)
        {
            overrideManagers.ToList().ForEach(pair => SetManager(pair.Key, pair.Value));
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="ProjectManagerFactory"/>.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use in the project managers.</param>
        public ProjectManagerFactory(IHttpClientFactory? httpClientFactory = null)
        {
            _projectManagers = CreateDefaultManagers(httpClientFactory);
        }

        public bool UnsetManager(string lookup)
        {
            return _projectManagers.Remove(lookup);
        }

        private void SetManager(string lookup, ConstructProjectManager generator)
        {
            _projectManagers[lookup] = generator;
        }

        public void ClearManagers()
        {
            _projectManagers.Clear();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpClientFactoryParam">The <see cref="IHttpClientFactory"/> to use in the project managers.</param>
        /// <returns>A dictionary of the default managers with associated constructors.</returns>
        private static Dictionary<string, ConstructProjectManager> CreateDefaultManagers(IHttpClientFactory? httpClientFactoryParam = null)
        {
            // If the httpClientFactory parameter is null, set the factory to the DefaultHttpClientFactory.
            IHttpClientFactory httpClientFactory = httpClientFactoryParam ?? new DefaultHttpClientFactory();
            return new Dictionary<string, ConstructProjectManager>(StringComparer.InvariantCultureIgnoreCase)
            {
                {
                    CargoProjectManager.Type, destinationDirectory =>
                        new CargoProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    CocoapodsProjectManager.Type, destinationDirectory =>
                        new CocoapodsProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    ComposerProjectManager.Type, destinationDirectory =>
                        new ComposerProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    CPANProjectManager.Type, destinationDirectory =>
                        new CPANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    CRANProjectManager.Type, destinationDirectory =>
                        new CRANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    GemProjectManager.Type, destinationDirectory =>
                        new GemProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    GitHubProjectManager.Type, destinationDirectory =>
                        new GitHubProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    GolangProjectManager.Type, destinationDirectory =>
                        new GolangProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    HackageProjectManager.Type, destinationDirectory =>
                        new HackageProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    MavenProjectManager.Type, destinationDirectory =>
                        new MavenProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    NPMProjectManager.Type, destinationDirectory =>
                        new NPMProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    NuGetProjectManager.Type, destinationDirectory =>
                        new NuGetProjectManager(destinationDirectory, new NuGetPackageActions(), httpClientFactory) // Add the NuGetPackageActions to the NuGetProjectManager.
                },
                {
                    PyPIProjectManager.Type, destinationDirectory =>
                        new PyPIProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    UbuntuProjectManager.Type, destinationDirectory =>
                        new UbuntuProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    URLProjectManager.Type, destinationDirectory =>
                        new URLProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    VSMProjectManager.Type, destinationDirectory =>
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
            ConstructProjectManager? projectManager = _projectManagers.GetValueOrDefault(purl.Type);

            return projectManager?.Invoke(destinationDirectory);
        }

        /// <summary>
        /// Add static method to just get a <see cref="BaseProjectManager"/> implementation.
        /// </summary>
        /// <param name="packageUrl">The <see cref="PackageURL"/> to get a project manager for.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to optionally add.</param>
        /// <returns>A new <see cref="BaseProjectManager"/> implementation.</returns>
        public static BaseProjectManager? GetProjectManager(PackageURL packageUrl, IHttpClientFactory? httpClientFactory = null)
        {
            return new ProjectManagerFactory(httpClientFactory).CreateProjectManager(packageUrl);
        }
    }
}