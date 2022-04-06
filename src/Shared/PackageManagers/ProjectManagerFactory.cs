// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    public class ProjectManagerFactory
    {
        /// <summary>
        /// This delegate takes the path to operate a project manager in and returns a new ProjectManager.
        /// </summary>
        /// <remarks>The only runtime parameter we need is the destination directory. Everything else can be defined in the constructor call itself.</remarks>
        /// <param name="destinationDirectory">The destination that any files should be saved to when downloading from this ProjectManager, defaults to the runtime directory.</param>
        /// <returns>An implementation of the <see cref="BaseProjectManager"/> class, or null if unable to construct.</returns>
        /// <example>
        /// destinationDirectory =>
        /// new NPMProjectManager(httpClientFactory, destinationDirectory)
        /// </example>
        /// <seealso cref="ProjectManagerFactory.CreateDefaultManagers">Example implementations in CreateDefaultManagers(IHttpClientFactory?)</seealso>
        public delegate BaseProjectManager? ConstructProjectManager(string destinationDirectory = ".");

        /// <summary>
        /// The dictionary of project managers.
        /// </summary>
        private readonly Dictionary<string, ConstructProjectManager> _projectManagers;

        /// <summary>
        /// Initializes a new instance of a <see cref="ProjectManagerFactory"/>.
        /// </summary>
        /// <param name="overrideManagers">
        /// If provided, will override each matching ProjectManager constructor from the defaults,
        /// or add it if it wasn't present in <see cref="CreateDefaultManagers"/>.
        /// </param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use in the project managers.</param>
        public ProjectManagerFactory(Dictionary<string, ConstructProjectManager> overrideManagers, IHttpClientFactory? httpClientFactory = null) : this(httpClientFactory)
        {
            foreach((string? type, ConstructProjectManager? ctor) in overrideManagers)
            {
                SetManager(type, ctor);
            }
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="ProjectManagerFactory"/>.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use in <see cref="CreateDefaultManagers"/>.</param>
        public ProjectManagerFactory(IHttpClientFactory? httpClientFactory = null)
        {
            _projectManagers = CreateDefaultManagers(httpClientFactory);
        }

        /// <summary>
        /// Sets a manager's constructor in the dictionary of constructors.
        /// </summary>
        /// <param name="type">The purl type of the ProjectManager to set.</param>
        /// <param name="ctor">The <see cref="ConstructProjectManager"/> to construct a new ProjectManager.</param>
        private void SetManager(string type, ConstructProjectManager ctor)
        {
            _projectManagers[type] = ctor;
        }

        /// <summary>
        /// Constructs the default set of ProjectManagers (optionally using a specified <see cref="IHttpClientFactory"/>) that ship with OSSGadget.
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
                        new NuGetProjectManager(httpClientFactory, destinationDirectory)
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
        /// Creates an appropriate project manager for a <see cref="PackageURL"/> given its <see cref="PackageURL.Type"/>.
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
        /// <param name="overrideManagers">
        /// If provided, will override each matching ProjectManager constructor from the defaults,
        /// or add it if it wasn't present in <see cref="CreateDefaultManagers"/>.
        /// </param>
        /// <param name="destinationDirectory">The new destination directory, if provided.</param>
        /// <returns>A new <see cref="BaseProjectManager"/> implementation.</returns>
        public static BaseProjectManager? ConstructPackageManager(PackageURL packageUrl, IHttpClientFactory? httpClientFactory = null, Dictionary<string, ConstructProjectManager>? overrideManagers = null, string destinationDirectory = ".")
        {
            if (overrideManagers != null && overrideManagers.Any())
            {
                return new ProjectManagerFactory(overrideManagers, httpClientFactory).CreateProjectManager(packageUrl, destinationDirectory);
            }
            return new ProjectManagerFactory(httpClientFactory).CreateProjectManager(packageUrl, destinationDirectory);
        }
    }
}