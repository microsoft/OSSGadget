// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    public class ProjectManagerFactory
    {

        private delegate BaseProjectManager? ConstructProjectManager(IHttpClientFactory httpClientFactory, string destinationDirectory = ".", IManagerProvider? managerProvider = null);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IManagerProvider? _nuGetProvider;
        
        /// <summary>
        /// The dictionary of project managers.
        /// </summary>
        private static readonly Dictionary<string, ConstructProjectManager> ProjectManagers =
            new(StringComparer.InvariantCultureIgnoreCase)
            {
                {
                    nameof(CargoProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new CargoProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(CocoapodsProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new CocoapodsProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(ComposerProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new ComposerProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(CPANProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new CPANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(CRANProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new CRANProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(GemProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new GemProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(GitHubProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new GitHubProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(GolangProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new GolangProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(HackageProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new HackageProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(MavenProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new MavenProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(NPMProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new NPMProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(NuGetProjectManager),
                    (httpClientFactory, destinationDirectory, managerProvider) =>
                        new NuGetProjectManager(httpClientFactory, destinationDirectory, managerProvider)
                },
                {
                    nameof(PyPIProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new PyPIProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(UbuntuProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new UbuntuProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(URLProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new URLProjectManager(httpClientFactory, destinationDirectory)
                },
                {
                    nameof(VSMProjectManager),
                    (httpClientFactory, destinationDirectory, _) =>
                        new VSMProjectManager(httpClientFactory, destinationDirectory)
                },
            };

        public ProjectManagerFactory(IHttpClientFactory? httpClientFactory = null, IManagerProvider? nuGetProvider = null)
        {
            _httpClientFactory = httpClientFactory ?? new DefaultHttpClientFactory();
            _nuGetProvider = nuGetProvider;
        }

        /// <summary>
        /// Creates an appropriate project manager for a package given its PackageURL.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> for the package to create the project manager for.</param>
        /// <param name="destinationDirectory">The new destination directory, if provided.</param>
        /// <param name="httpClientFactory"></param>
        /// <param name="nuGetProvider"></param>
        /// <returns>The implementation of <see cref="BaseProjectManager"/> for this <paramref name="purl"/>'s type.</returns>
        public virtual BaseProjectManager? CreateProjectManager(PackageURL purl, string destinationDirectory = ".", IHttpClientFactory? httpClientFactory = null, IManagerProvider? nuGetProvider = null)
        {
            ConstructProjectManager? projectManager = ProjectManagers.GetValueOrDefault($"{purl.Type}ProjectManager");

            return projectManager?.Invoke(httpClientFactory ?? this._httpClientFactory, destinationDirectory, nuGetProvider ?? this._nuGetProvider);
        }

        /// <summary>
        /// Add static method to just get a project manager for one time use.
        /// </summary>
        /// <param name="packageUrl">The <see cref="PackageURL"/> to get a project manager for.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to optionally add.</param>
        /// <returns>A new project manager.</returns>
        public static BaseProjectManager? GetProjectManager(PackageURL packageUrl, IHttpClientFactory? httpClientFactory = null)
        {
            return new ProjectManagerFactory(httpClientFactory).CreateProjectManager(packageUrl);
        }
    }
}