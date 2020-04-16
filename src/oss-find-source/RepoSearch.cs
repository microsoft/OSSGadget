using Microsoft.CST.OpenSource.Shared;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource
{
    /// <summary>
    /// Find out the source code repository for a given package, by applying various algorithms.
    /// 
    /// How it works:
    ///    try getting the repo information from npm registry by looking at version/repository
    ///        If the package doesnt have it, try searching the metadata for any possible repo urls
    ///        TODO:
    ///        If that didnt work, try searching github for a repo with same name
    ///            if we find a single repo with an exact name, return it
    ///            if we find multiple repos with the exact name, check if all of these are forks
    ///                if they are not forks, check for metrics like activity, code changes, and pick the one which is highest
    ///
    /// also calculate a probability of the repo we found being the right one and return it
    /// Attributes:
    /// None
    /// Caveats:
    ///     Does not work very well with monorepos
    ///     Lower confidence scores may not point to right repos
    ///     There is no verification done to ensure that the source repo found was for the package
    ///     Cannot be absolutely certain about the source repo without manual intervention
    /// </summary>
    public class RepoSearch
    {
        /// <summary>
        /// Class logger
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// try to resolve the source code for an npm package through different means
        ///     1) Look at the metadata
        ///     2) Try searching github
        ///     3) Try calculating metrics for same name repos
        /// </summary>
        /// <param name="github_apikey"></param>
        /// <param name="package_name"></param>
        /// <returns></returns>
        public async Task<Dictionary<PackageURL, float>> ResolvePackageLibraryAsync(PackageURL purl)
        {
            var purlNoVersion = new PackageURL(purl.Type, purl.Namespace, purl.Name,
                                   null, purl.Qualifiers, purl.Subpath);
            Logger.Debug("Searching for source code for {0}", purlNoVersion.ToString());

            // Use reflection to find the correct downloader class
            var projectManagerClass = typeof(BaseProjectManager).Assembly.GetTypes()
               .Where(type => type.IsSubclassOf(typeof(BaseProjectManager)))
               .Where(type => type.Name.Equals($"{purl.Type}ProjectManager",
                                               StringComparison.InvariantCultureIgnoreCase))
               .FirstOrDefault();

            Dictionary<PackageURL, float> repoMappings = null;
            if (projectManagerClass != null)
            {
                var ctor = projectManagerClass.GetConstructor(Array.Empty<Type>());
                var projectManager = (BaseProjectManager)(ctor.Invoke(Array.Empty<object>()));
                repoMappings = await projectManager.SearchMetadata(purl);

                if (repoMappings.Count == 0)
                {
                    Logger.Info("Could not figure out the repository from the package metadata");
                }
            }
            else
            {
                throw new ArgumentException("Invalid Package URL type: {0}", purlNoVersion.Type);
            }

            return repoMappings;
        }


    }
}
