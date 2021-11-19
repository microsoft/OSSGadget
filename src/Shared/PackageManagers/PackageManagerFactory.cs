// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Shared
{
    public static class ProjectManagerFactory
    {
        /// <summary>
        /// Create a BaseProjectManager.
        /// </summary>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns></returns>
        public static BaseProjectManager CreateBaseProjectManager(string destinationDirectory)
        {
            return new BaseProjectManager(destinationDirectory);
        }

        /// <summary>
        ///     Get an appropriate project manager for package given its PackageURL.
        /// </summary>
        /// <param name="purl"> </param>
        /// <param name="destinationDirectory">The directory to use to store any downloaded packages.</param>
        /// <returns> BaseProjectManager object </returns>
        public static BaseProjectManager? CreateProjectManager(PackageURL purl, string? destinationDirectory = null)
        {
            if (projectManagers.Count == 0)
            {
                projectManagers.AddRange(typeof(BaseProjectManager).Assembly.GetTypes()
               .Where(type => type.IsSubclassOf(typeof(BaseProjectManager))));
            }
            // Use reflection to find the correct package management class
            var downloaderClass = projectManagers
               .Where(type => type.Name.Equals($"{purl.Type}ProjectManager",
                                               StringComparison.InvariantCultureIgnoreCase))
               .FirstOrDefault();
            if (downloaderClass != null)
            {
                var ctor = downloaderClass.GetConstructor(new Type[] { typeof(string) });
                if (ctor != null)
                {
                    var _downloader = (BaseProjectManager)(ctor.Invoke(new object?[] { destinationDirectory }));
                    return _downloader;
                }
            }

            return null;
        }

        // do reflection only once
        private static List<Type> projectManagers = new List<Type>();
    }
}