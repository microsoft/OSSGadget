// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Model.Providers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    public class ProjectManagerFactory
    {
        /// <summary>
        /// The dictionary of project managers.
        /// </summary>
        private readonly Dictionary<string, BaseProjectManager> _projectManagers = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// The directory to save files to.
        /// Defaults to the directory the code is running in.
        /// </summary>
        protected string Directory { get; } = ".";

        public ProjectManagerFactory(IHttpClientFactory? httpClientFactory = null, string? destinationDirectory = null, IManagerProvider? nuGetProvider = null)
        {
            httpClientFactory ??= new DefaultHttpClientFactory();
            if (destinationDirectory is not null)
            {
                Directory = destinationDirectory;
            }
            
            // Add all of the project managers. Kinda like a "Hardcoded Dependency Injection".
            this._projectManagers.Add(nameof(CargoProjectManager), new CargoProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(CocoapodsProjectManager), new CocoapodsProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(ComposerProjectManager), new ComposerProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(CPANProjectManager), new CPANProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(CRANProjectManager), new CRANProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(GemProjectManager), new GemProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(GitHubProjectManager), new GitHubProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(GolangProjectManager), new GolangProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(HackageProjectManager), new HackageProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(MavenProjectManager), new MavenProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(NPMProjectManager), new NPMProjectManager(httpClientFactory, Directory));

            // NuGet gets the provider as well
            this._projectManagers.Add(nameof(NuGetProjectManager), new NuGetProjectManager(httpClientFactory, Directory, nuGetProvider));

            this._projectManagers.Add(nameof(PyPIProjectManager), new PyPIProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(UbuntuProjectManager), new UbuntuProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(URLProjectManager), new URLProjectManager(httpClientFactory, Directory));
            this._projectManagers.Add(nameof(VSMProjectManager), new VSMProjectManager(httpClientFactory, Directory));
        }

        /// <summary>
        /// Get an appropriate project manager for package given its PackageURL. And update it's directory if provided.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> for the package to create the project manager for.</param>
        /// <param name="destinationDirectory">The new destination directory, if provided.</param>
        /// <returns>The implementation of <see cref="BaseProjectManager"/> for this <paramref name="purl"/>'s type.</returns>
        public BaseProjectManager? GetProjectManager(PackageURL purl, string? destinationDirectory = null)
        {
            BaseProjectManager? projectManager = this._projectManagers.GetValueOrDefault($"{purl.Type}ProjectManager");
            if (projectManager is not null && !string.IsNullOrWhiteSpace(destinationDirectory))
            {
                projectManager.TopLevelExtractionDirectory = destinationDirectory;
            }

            return projectManager;
        }

        public static BaseProjectManager? CreateProjectManager(PackageURL packageUrl, IHttpClientFactory? httpClientFactory = null)
        {
            return new ProjectManagerFactory(httpClientFactory).GetProjectManager(packageUrl);
        }
    }
}