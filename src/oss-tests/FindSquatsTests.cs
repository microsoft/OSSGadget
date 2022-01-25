// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.FindSquats;
using Microsoft.CST.OpenSource.FindSquats.ExtensionMethods;
using Microsoft.CST.OpenSource.FindSquats.Mutators;
using Microsoft.CST.OpenSource.PackageManagers;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
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
                            if (mutation.Mutated.Equals(expectedToFind))
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
        [DataRow("pkg:npm/foo", "\"SquattedPackage\":{\"Name\":\"too\",")]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", "\"SquattedPackage\":{\"Name\":\"microsoft.cst.oat.net\",")]
        public void ConvertToJson(string packageUrl, string expectedToFind)
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
                            if (result.ToJson().Contains(expectedToFind))
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
