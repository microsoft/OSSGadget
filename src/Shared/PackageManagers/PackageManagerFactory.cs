using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Shared
{
    public class ProjectManagerFactory
    {
        // do reflection only once
        static List<Type> projectManagers = new List<Type>();

        /// <summary>
        /// Get the project manager for the package type
        /// </summary>
        /// <param name="purl"></param>
        /// <returns>BaseProjectManager object</returns>
        public static BaseProjectManager CreateProjectManager(PackageURL purl, string destinationDirectory)
        {
            if(projectManagers.Count == 0)
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
                var ctor = downloaderClass.GetConstructor(Array.Empty<Type>());
                var _downloader = (BaseProjectManager)(ctor.Invoke(Array.Empty<object>()));

                // TODO: find a better way to do this, preferably as constructor argument
                _downloader.TopLevelExtractionDirectory = destinationDirectory;
                return _downloader;
            }

            return null;
        }
    }
}
