// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats
{
    using Contracts;
    using ExtensionMethods;
    using Microsoft.CST.OpenSource;
    using Microsoft.CST.OpenSource.Exceptions;
    using Microsoft.CST.OpenSource.Helpers;
    using Microsoft.CST.OpenSource.PackageManagers;
    using Mutators;
    using PackageUrl;
    using System.Collections.Generic;
    using System.Linq;

    public class FindPackageSquats : OssGadgetLib
    {
        /// <summary>
        /// The <see cref="PackageURL"/> to find squats for.
        /// </summary>
        public PackageURL PackageUrl { get; }

        public IBaseProjectManager? ProjectManager { get;  }

        public FindPackageSquats(ProjectManagerFactory projectManagerFactory, PackageURL packageUrl)
            : base(projectManagerFactory)
        {
            PackageUrl = packageUrl;
            ProjectManager = projectManagerFactory.CreateProjectManager(packageUrl);
            if (ProjectManager is null)
            {
                Logger.Trace($"Could not generate valid ProjectManager from { packageUrl }.");
                throw new InvalidProjectManagerException(packageUrl);
            }
        }

        public IDictionary<string, IList<Mutation>>? GenerateSquatCandidates(IEnumerable<IMutator>? mutators = null,
            MutateOptions? options = null)
        {
            if (ProjectManager is null)
            {
                Check.NotNull(nameof(ProjectManager), ProjectManager);
            }
            else
            {
                if (mutators != null)
                {
                    return ProjectManager.EnumerateSquatCandidates(PackageUrl, mutators, options);
                }
                return ProjectManager.EnumerateSquatCandidates(PackageUrl, options);
            }
            return null;
        }

        public IAsyncEnumerable<FindPackageSquatResult> FindExistingSquatsAsync(IDictionary<string, IList<Mutation>>? candidateMutations, MutateOptions? options = null)
        {
            return ProjectManager?.EnumerateExistingSquatsAsync(PackageUrl, candidateMutations, options) ?? AsyncEnumerable.Empty<FindPackageSquatResult>();
        }
    }
}