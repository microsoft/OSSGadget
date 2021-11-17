using Microsoft.CST.OpenSource.FindSquats.Mutators;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class FindSquatResult
    {
        public FindSquatResult(string packageName, PackageURL packageUrl, PackageURL squattedPackage, IEnumerable<Mutation> mutations)
        {
            PackageName = packageName;
            PackageUrl = packageUrl;
            SquattedPackage = squattedPackage;
            Mutations = mutations;
        }
        public string PackageName { get; }
        public PackageURL PackageUrl { get; }
        public PackageURL SquattedPackage { get; }
        public IEnumerable<string> Rules => Mutations.Select(x => x.Reason);
        public IEnumerable<Mutation> Mutations { get; }
    }
}
