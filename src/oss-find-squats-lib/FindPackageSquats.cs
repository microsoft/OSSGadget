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
            this.PackageUrl = packageUrl;
            this.ProjectManager = ProjectManagerFactory.CreateProjectManager(packageUrl, httpClientFactory);
            if (ProjectManager is null)
            {
                Logger.Trace($"Could not generate valid ProjectManager from { packageUrl }.");
                throw new InvalidProjectManagerException(packageUrl);
            }
        }

        public IDictionary<string, IList<Mutation>>? GenerateSquats(IEnumerable<IMutator>? mutators = null,
            MutateOptions? options = null)
        {
            Check.NotNull(nameof(this.ProjectManager), this.ProjectManager);
            if (mutators != null)
            {
                return this.ProjectManager?.EnumerateSquats(this.PackageUrl, mutators, options);
            }
            return this.ProjectManager?.EnumerateSquats(this.PackageUrl, options);
        }

        public IAsyncEnumerable<FindPackageSquatResult> FindExistingSquatsAsync(IDictionary<string, IList<Mutation>>? candidateMutations, MutateOptions? options = null)
        {
            return this.ProjectManager?.EnumerateExistingSquatsAsync(this.PackageUrl, candidateMutations, options) ?? AsyncEnumerable.Empty<FindPackageSquatResult>();
        }
    }
}