// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using PackageActions;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    public class ProjectManagerFactory : IProjectManagerFactory
    {
        /// <summary>
        /// This delegate takes the path to operate a project manager in and returns a new ProjectManager.
        /// </summary>
        /// <remarks>The only runtime parameter we need is the destination directory. Everything else can be defined in the constructor call itself.</remarks>
        /// <param name="destinationDirectory">The destination that any files should be saved to when downloading from this ProjectManager, defaults to the runtime directory.</param>
        /// <param name="timeout">The <see cref="TimeSpan"/> to wait before the request times out.</param>
        /// <returns>An implementation of the <see cref="BaseProjectManager"/> class, or null if unable to construct.</returns>
        /// <example>
        /// destinationDirectory =>
        /// new NPMProjectManager(httpClientFactory, destinationDirectory)
        /// </example>
        /// <seealso cref="ProjectManagerFactory.GetDefaultManagers">Example implementations in GetDefaultManagers(IHttpClientFactory?)</seealso>
        public delegate BaseProjectManager? ConstructProjectManager(string destinationDirectory = ".", TimeSpan? timeout = null);

        /// <summary>
        /// The dictionary of project managers.
        /// </summary>
        private readonly Dictionary<string, ConstructProjectManager> _projectManagers;

        /// <summary>
        /// Initializes a new instance of a <see cref="ProjectManagerFactory"/>.
        /// </summary>
        /// <param name="overrideManagers"> If provided, will set the project manager dictionary instead of using the defaults.</param>
        public ProjectManagerFactory(Dictionary<string, ConstructProjectManager> overrideManagers)
        {
            _projectManagers = overrideManagers;
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="ProjectManagerFactory"/>.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use in <see cref="GetDefaultManagers"/>.</param>
        /// <param name="allowUseOfRateLimitedRegistryAPIs"> The Cargo Project Manager uses this flag to disable the use of a ratelimited API for 
        /// fetching metadata when the user scenario requires retrieving metadata for more than one package per second. </param>
        public ProjectManagerFactory(IHttpClientFactory? httpClientFactory = null, bool allowUseOfRateLimitedRegistryAPIs = true)
        {
            _projectManagers = GetDefaultManagers(httpClientFactory, allowUseOfRateLimitedRegistryAPIs);
        }

        /// <summary>
        /// Constructs the default set of ProjectManagers (optionally using a specified <see cref="IHttpClientFactory"/>) that ship with OSSGadget.
        /// </summary>
        /// <param name="httpClientFactoryParam">The <see cref="IHttpClientFactory"/> to use in the project managers.</param>
        /// <returns>A dictionary of the default managers with associated constructors.</returns>
        public static Dictionary<string, ConstructProjectManager> GetDefaultManagers(IHttpClientFactory? httpClientFactoryParam = null, bool allowUseOfRateLimitedRegistryAPIs = true)
        {
            // If the httpClientFactory parameter is null, set the factory to the DefaultHttpClientFactory.
            IHttpClientFactory httpClientFactory = httpClientFactoryParam ?? new DefaultHttpClientFactory();

            return new Dictionary<string, ConstructProjectManager>(StringComparer.InvariantCultureIgnoreCase)
            {
                {
                    CargoProjectManager.Type, (destinationDirectory, timeout) =>
                        new CargoProjectManager(destinationDirectory, new NoOpPackageActions(), httpClientFactory, timeout, allowUseOfRateLimitedRegistryAPIs)
                },
                {
                    CocoapodsProjectManager.Type, (destinationDirectory, timeout) =>
                        new CocoapodsProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    ComposerProjectManager.Type, (destinationDirectory, timeout) =>
                        new ComposerProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    CondaProjectManager.Type, (destinationDirectory, timeout) =>
                        new CondaProjectManager(destinationDirectory, new NoOpPackageActions(), httpClientFactory, timeout)
                },
                {
                    CPANProjectManager.Type, (destinationDirectory, timeout) =>
                        new CPANProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    CRANProjectManager.Type, (destinationDirectory, timeout) =>
                        new CRANProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    GemProjectManager.Type, (destinationDirectory, timeout) =>
                        new GemProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    GitHubProjectManager.Type, (destinationDirectory, timeout) =>
                        new GitHubProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    GolangProjectManager.Type, (destinationDirectory, timeout) =>
                        new GolangProjectManager(destinationDirectory, new NoOpPackageActions(), httpClientFactory, timeout)
                },
                {
                    HackageProjectManager.Type, (destinationDirectory, timeout) =>
                        new HackageProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    MavenProjectManager.Type, (destinationDirectory, timeout) =>
                        new MavenProjectManager(destinationDirectory, new NoOpPackageActions(), httpClientFactory, timeout)
                },
                {
                    NPMProjectManager.Type, (destinationDirectory, timeout) =>
                        new NPMProjectManager(destinationDirectory, new NoOpPackageActions(), httpClientFactory, timeout)
                },
                {
                    NuGetProjectManager.Type, (destinationDirectory, timeout) =>
                        new NuGetProjectManager(destinationDirectory, new NuGetPackageActions(), httpClientFactory, timeout)
                },
                {
                    PyPIProjectManager.Type, (destinationDirectory, timeout) =>
                        new PyPIProjectManager(destinationDirectory, new NoOpPackageActions(), httpClientFactory, timeout)
                },
                {
                    UbuntuProjectManager.Type, (destinationDirectory, timeout) =>
                        new UbuntuProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    URLProjectManager.Type, (destinationDirectory, timeout) =>
                        new URLProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
                {
                    VSMProjectManager.Type, (destinationDirectory, timeout) =>
                        new VSMProjectManager(httpClientFactory, destinationDirectory, timeout)
                },
            };
        }

        /// <inheritdoc />
        public IBaseProjectManager? CreateProjectManager(PackageURL purl, string destinationDirectory = ".", TimeSpan? timeout = null)
        {
            ConstructProjectManager? projectManager = _projectManagers.GetValueOrDefault(purl.Type);

            return projectManager?.Invoke(destinationDirectory, timeout);
        }

        /// <summary>
        /// Add static method to just get a <see cref="BaseProjectManager"/> implementation.
        /// </summary>
        /// <param name="packageUrl">The <see cref="PackageURL"/> to get a project manager for.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to optionally add.</param>
        /// <param name="overrideManagers"> If provided, will set the project manager dictionary instead of using the defaults.</param>
        /// <param name="destinationDirectory">The new destination directory, if provided.</param>
        /// <returns>A new <see cref="BaseProjectManager"/> implementation.</returns>
        public static IBaseProjectManager? ConstructPackageManager(PackageURL packageUrl, IHttpClientFactory? httpClientFactory = null, Dictionary<string, ConstructProjectManager>? overrideManagers = null, string destinationDirectory = ".")
        {
            if (overrideManagers != null && overrideManagers.Any())
            {
                return new ProjectManagerFactory(overrideManagers).CreateProjectManager(packageUrl, destinationDirectory);
            }
            return new ProjectManagerFactory(httpClientFactory).CreateProjectManager(packageUrl, destinationDirectory);
        }
    }
}