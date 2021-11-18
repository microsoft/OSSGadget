using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Shared
{
    public static class ProjectManagerFactory
    {
        public static BaseProjectManager CreateBaseProjectManager(string destinationDirectory)
        {
            return new BaseProjectManager(destinationDirectory);
        }

        /// <summary>
        ///     Get the project manager for the package type
        /// </summary>
        /// <param name="purl"> </param>
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