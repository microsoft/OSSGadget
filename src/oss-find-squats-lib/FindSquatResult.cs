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
        public FindSquatResult(string packageName, PackageURL packageUrl, PackageURL squattedPackage, IEnumerable<string> rules)
        {
            PackageName = packageName;
            PackageUrl = packageUrl;
            SquattedPackage = squattedPackage;
            Rules = rules;
        }
        public string PackageName { get; }
        public PackageURL PackageUrl { get; }
        public PackageURL SquattedPackage { get; }
        public IEnumerable<string> Rules { get; }
    }
}
