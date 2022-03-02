// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.FindSquats;
using Microsoft.CST.OpenSource.FindSquats.ExtensionMethods;
using Microsoft.CST.OpenSource.FindSquats.Mutators;
using Microsoft.CST.OpenSource.PackageManagers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    using PackageUrl;

    [TestClass]
    public class FindSquatsTest
    {
        [DataTestMethod]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", false)]
        [DataRow("pkg:npm/microsoft/microsoft-graph-library", false)]
        [DataRow("pkg:npm/foo", true)]
        public async Task DetectSquats(string packageUrl, bool expectedToHaveSquats)
        {
            FindSquatsTool fst = new();
            FindSquatsTool.Options options = new()
            {
                Quiet = true,
                Targets = new string[] { packageUrl }
            };
            (string output, int numSquats) result = await fst.RunAsync(options);
            Assert.IsTrue(expectedToHaveSquats ? result.numSquats > 0 : result.numSquats == 0);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/%40angular/core", "@engular/core", "@angullar/core")]
        [DataRow("pkg:npm/lodash", "odash", "lodah")]
        [DataRow("pkg:npm/%40babel/runtime", "@abel/runtime", "@bable/runtime")]
        public void ScopedNpmPackageSquats(string packageUrl, params string[] expectedSquats)
        {
            FindPackageSquats findPackageSquats =
                new(new DefaultHttpClientFactory(), new PackageURL(packageUrl));

            IDictionary<string, IList<Mutation>>? squatsCandidates = findPackageSquats.GenerateSquatCandidates();

            Assert.IsNotNull(squatsCandidates);

            foreach (string expectedSquat in expectedSquats)
            {
                Assert.IsTrue(squatsCandidates.ContainsKey(expectedSquat));
            }
        }

        [DataTestMethod]
        [DataRow("pkg:npm/foo", "foojs")] // SuffixAdded, js
        [DataRow("pkg:npm/lodash", "odash")] // RemovedCharacter, first character
        [DataRow("pkg:nuget/Microsoft.CST.OAT", "microsoft.cst.oat.net")]
        public void GenerateManagerSpecific(string packageUrl, string expectedToFind)
        {
            PackageURL purl = new(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                BaseProjectManager? manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                if (manager is not null)
                {
                    foreach (IMutator mutator in manager.GetDefaultMutators())
                    {
                        foreach (Mutation mutation in mutator.Generate(purl.Name))
                        {
                            if (mutation.Mutated.Equals(expectedToFind, StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }
            Assert.Fail($"Did not find expected mutation {expectedToFind}");
        }

        [DataTestMethod]
        [DataRow("pkg:npm/foo", "pkg:npm/too")]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", "pkg:nuget/microsoft.cst.oat.net")]
        public void ConvertToAndFromJson(string packageUrl, string expectedToFind)
        {
            PackageURL purl = new(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                BaseProjectManager? manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                if (manager is not null)
                {
                    foreach (IMutator mutator in manager.GetDefaultMutators())
                    {
                        foreach (Mutation mutation in mutator.Generate(purl.Name))
                        {
                            FindPackageSquatResult result = new(purl.Name, purl,
                                new PackageURL(purl.Type, mutation.Mutated), new[] { mutation });
                            string jsonResult = result.ToJson();
                            if (jsonResult.Contains(expectedToFind, StringComparison.OrdinalIgnoreCase))
                            {
                                FindPackageSquatResult fromJson = FindPackageSquatResult.FromJsonString(jsonResult);
                                if (fromJson.PackageName.Equals(purl.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            Assert.Fail($"Did not find expected mutation {expectedToFind}");
        }

        [DataTestMethod]
        [DataRow("pkg:npm/foo", typeof(UnicodeHomoglyphMutator))]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", typeof(UnicodeHomoglyphMutator))]
        public void DontGenerateManagerSpecific(string packageUrl, Type notExpectedToFind)
        {
            PackageURL purl = new(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                BaseProjectManager? manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                if (manager is not null)
                {
                    foreach (IMutator mutator in manager.GetDefaultMutators())
                    {
                        if (mutator.GetType().Equals(notExpectedToFind))
                        {
                            Assert.Fail($"Found unexpected mutator {notExpectedToFind.FullName}");
                        }
                    }
                }
            }
        }
    }
}
