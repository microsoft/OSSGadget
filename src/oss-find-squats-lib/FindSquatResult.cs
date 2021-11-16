// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.CST.OpenSource.Shared;
using System.Collections.Generic;

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
