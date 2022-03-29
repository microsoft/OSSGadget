// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers
{
    using Contracts;
    using Model.Providers;
    using PackageUrl;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    public static class ProjectManagerFactory
    {
        /// <summary>
        /// Create a BaseProjectManager.
        /// </summary>
        /// <param name="managerProvider">The <see cref="IManagerProvider{IManagerMetadata}"/> to for this project manager.</param>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns></returns>
        public static BaseProjectManager CreateBaseProjectManager(IManagerProvider managerProvider, string destinationDirectory)
        {
            return new BaseProjectManager(managerProvider, destinationDirectory);
        }

        /// <summary>
        /// Create a BaseProjectManager.
        /// </summary>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns></returns>
        public static BaseProjectManager CreateBaseProjectManager(string destinationDirectory)
        {
            return new BaseProjectManager(new BaseProvider(), destinationDirectory);
        }

        /// <summary>
        /// Get an appropriate project manager for package given its PackageURL.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> for the package to create the project manager for.</param>
        /// <param name="managerProviderFactory"> The <see cref="IManagerProviderFactory"/> for the project manager to use for making the package manager's provider.</param>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns>A new <see cref="BaseProjectManager"/> object implementation.</returns>
        public static BaseProjectManager? CreateProjectManager(PackageURL purl, IManagerProviderFactory? managerProviderFactory = null, string? destinationDirectory = null)
        {
            if (projectManagers.Count == 0)
            {
                projectManagers.AddRange(typeof(BaseProjectManager).Assembly.GetTypes()
               .Where(type => type.IsSubclassOf(typeof(BaseProjectManager))));
            }

            // Use reflection to find the correct package management class
            Type? managerClass = projectManagers
                .FirstOrDefault(type => type.Name.Equals($"{purl.Type}ProjectManager",
                    StringComparison.InvariantCultureIgnoreCase));

            BaseProvider provider = managerProviderFactory?.CreateProvider(purl) ?? new BaseProvider();

            if (managerClass != null)
            {
                System.Reflection.ConstructorInfo? ctor = managerClass.GetConstructor(new[] { typeof(IManagerProvider), typeof(string) });
                if (ctor != null)
                {
                    BaseProjectManager? projectManager = (BaseProjectManager)ctor.Invoke(new object?[] { provider, destinationDirectory });
                    return projectManager;
                }
            }

            return null;
        }

        // do reflection only once
        private static readonly List<Type> projectManagers = new();
    }
}