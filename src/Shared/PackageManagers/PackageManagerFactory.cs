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
        /// <param name="httpClientFactory">The Http cliend factory for making http clients to make requests with.</param>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns></returns>
        public static BaseProjectManager CreateBaseProjectManager(IHttpClientFactory httpClientFactory, IManagerProvider<IManagerMetadata> provider, string destinationDirectory)
        {
            return new BaseProjectManager(httpClientFactory, provider, destinationDirectory);
        }

        /// <summary>
        /// Create a BaseProjectManager.
        /// </summary>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns></returns>
        public static BaseProjectManager CreateBaseProjectManager(IManagerProvider<IManagerMetadata> provider, string destinationDirectory)
        {
            return new BaseProjectManager(provider, destinationDirectory);
        }

        /// <summary>
        /// Get an appropriate project manager for package given its PackageURL.
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> for the package to create the project manager for.</param>
        /// <param name="httpClientFactory"> The <see cref="IHttpClientFactory"/> for the project manager to use for making Http Clients to make web requests.</param>
        /// <param name="managerProvider">The <see cref="IManagerProvider{IManagerMetadata}"/> for this manager.</param>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns> BaseProjectManager object </returns>
        public static BaseProjectManager? CreateProjectManager(PackageURL purl, IHttpClientFactory? httpClientFactory = null, IManagerProvider<IManagerMetadata>? managerProvider = null, string? destinationDirectory = null)
        {
            if (projectManagers.Count == 0)
            {
                projectManagers.AddRange(typeof(BaseProjectManager).Assembly.GetTypes()
               .Where(type => type.IsSubclassOf(typeof(BaseProjectManager))));
            }
            // Use reflection to find the correct package management class
            Type? downloaderClass = projectManagers
                .FirstOrDefault(type => type.Name.Equals($"{purl.Type}ProjectManager",
                    StringComparison.InvariantCultureIgnoreCase));

            if (downloaderClass != null)
            {
                if (httpClientFactory != null)
                {
                    if (managerProvider != null)
                    {
                        System.Reflection.ConstructorInfo? ctor = downloaderClass.GetConstructor(new[] { typeof(IHttpClientFactory), typeof(IManagerProvider<IManagerMetadata>), typeof(string) });
                        if (ctor != null)
                        {
                            BaseProjectManager? _downloader = (BaseProjectManager)ctor.Invoke(new object?[] { httpClientFactory, managerProvider, destinationDirectory });
                            return _downloader;
                        }
                    }
                    else
                    {
                        System.Reflection.ConstructorInfo? ctor = downloaderClass.GetConstructor(new[] { typeof(IHttpClientFactory), typeof(string) });
                        if (ctor != null)
                        {
                            BaseProjectManager? _downloader = (BaseProjectManager)ctor.Invoke(new object?[] { httpClientFactory, destinationDirectory });
                            return _downloader;
                        }
                    }
                }
                else
                {
                    if (managerProvider != null)
                    {
                        System.Reflection.ConstructorInfo? ctor = downloaderClass.GetConstructor(new[] { typeof(IManagerProvider<IManagerMetadata>), typeof(string) });
                        if (ctor != null)
                        {
                            BaseProjectManager? _downloader = (BaseProjectManager)ctor.Invoke(new object?[] { managerProvider, destinationDirectory });
                            return _downloader;
                        }
                    }
                    else
                    {
                        System.Reflection.ConstructorInfo? ctor = downloaderClass.GetConstructor(new[] { typeof(string) });
                        if (ctor != null)
                        {
                            BaseProjectManager? _downloader = (BaseProjectManager)ctor.Invoke(new object?[] { destinationDirectory });
                            return _downloader;
                        }
                    }
                }

            }

            return null;
        }

        // do reflection only once
        private static readonly List<Type> projectManagers = new();
    }
}