using Microsoft.CST.OpenSource.FindSquats;
using Microsoft.CST.OpenSource.FindSquats.ExtensionMethods;
using Microsoft.CST.OpenSource.FindSquats.Mutators;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Tests
{
    [TestClass]
    public class FindSquatsTest
    {
        public FindSquatsTest()
        {
            CommonInitialization.Initialize();
        }

        [DataTestMethod]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", false)]
        [DataRow("pkg:npm/microsoft/microsoft-graph-library", false)]
        [DataRow("pkg:npm/foo", true)]
        public async Task DetectSquats(string packageUrl, bool expectedToHaveSquats)
        {
            var fst = new FindSquatsTool();
            var options = new FindSquatsTool.Options()
            {
                Quiet = true,
                Targets = new string[] { packageUrl }
            };
            var result = await fst.RunAsync(options);
            Assert.IsTrue(expectedToHaveSquats ? result.numSquats > 0 : result.numSquats == 0);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/foo", "foojs")]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", "microsoft.cst.oat.net")]
        public async Task GenerateManagerSpecific(string packageUrl, string expectedToFind)
        {
            var purl = new PackageURL(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                var manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                if (manager is not null)
                {
                    foreach (var mutator in manager.GetDefaultMutators())
                    {
                        foreach (var mutation in mutator.Generate(purl.Name))
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
        [DataRow("pkg:npm/foo", typeof(UnicodeHomoglyphMutator))]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", typeof(UnicodeHomoglyphMutator))]
        public async Task DontGenerateManagerSpecific(string packageUrl, Type notExpectedToFind)
        {
            var purl = new PackageURL(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                var manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                if (manager is not null)
                {
                    foreach (var mutator in manager.GetDefaultMutators())
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
