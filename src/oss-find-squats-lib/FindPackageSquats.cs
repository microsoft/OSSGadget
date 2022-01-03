// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats
{
    using ExtensionMethods;
    using Lib.Exceptions;
    using Lib.Helpers;
    using Lib.PackageManagers;
    using Microsoft.CST.OpenSource.Lib;
    using Mutators;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    public class FindPackageSquats : OssGadgetLib
    {

        /// <summary>
        /// The <see cref="PackageURL"/> to find squats for.
        /// </summary>
        public PackageURL PackageUrl { get; }

        public BaseProjectManager? ProjectManager { get;  }

        public FindPackageSquats(IHttpClientFactory httpClientFactory, PackageURL packageUrl, string directory = ".")
            : base(httpClientFactory, directory)
        {
            PackageUrl = packageUrl;
            ProjectManager = ProjectManagerFactory.CreateProjectManager(packageUrl, httpClientFactory);
            if (ProjectManager is null)
            {
                Logger.Trace($"Could not generate valid ProjectManager from { packageUrl }.");
                throw new InvalidProjectManagerException(packageUrl);
            }
        }

        public IDictionary<string, IList<Mutation>>? GenerateSquats(IEnumerable<IMutator>? mutators = null,
            MutateOptions? options = null)
        {
            Check.NotNull(nameof(ProjectManager), ProjectManager);
            if (mutators != null)
            {
                return ProjectManager?.EnumerateSquats(PackageUrl, mutators, options);
            }
            return ProjectManager?.EnumerateSquats(PackageUrl, options);
        }

        public IAsyncEnumerable<FindPackageSquatResult> FindExistingSquatsAsync(IDictionary<string, IList<Mutation>>? candidateMutations, MutateOptions? options = null)
        {
            return ProjectManager?.EnumerateExistingSquatsAsync(PackageUrl, candidateMutations, options) ?? AsyncEnumerable.Empty<FindPackageSquatResult>();
        }
    }
}