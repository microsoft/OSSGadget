﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

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
    using Contracts;
    using Extensions;
    using Helpers;
    using Model.Metadata;
    using PackageUrl;
    using System.Linq;
    using System.Net.Http;
    using System.Web;

    [TestClass]
    public class FindSquatsTest
    {
        [DataTestMethod]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", false, false)]
        [DataRow("pkg:npm/microsoft/microsoft-graph-library", false, false)]
        [DataRow("pkg:npm/foo", true, true)]
        public async Task DetectSquats(string packageUrl, bool generateSquats, bool expectedToHaveSquats)
        {
            PackageURL purl = new(packageUrl);

            IEnumerable<string>? validSquats = null;
            
            // Only populate validSquats if we want to generate squats.
            if (generateSquats)
            {
                validSquats = ProjectManagerFactory.GetDefaultManagers()[purl.Type].Invoke()?.EnumerateSquatCandidates(purl)?.Keys;
            }

            // Construct the mocked IHttpClientFactory
            IHttpClientFactory httpClientFactory = FindSquatsHelper.SetupHttpCalls(purl: purl, validSquats: validSquats);
            
            // Construct the manager overrides with the mocked IHttpClientFactory.
            Dictionary<string, ProjectManagerFactory.ConstructProjectManager> managerOverrides = ProjectManagerFactory.GetDefaultManagers(httpClientFactory);

            IManagerPackageActions<NuGetPackageVersionMetadata>? nugetPackageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(
                purl);

            // Override the NuGet constructor to add the mocked NuGetPackageActions.
            managerOverrides[NuGetProjectManager.Type] =
                _ => new NuGetProjectManager(".", nugetPackageActions, httpClientFactory);
            
            ProjectManagerFactory projectManagerFactory = new(managerOverrides);
            FindSquatsTool fst = new(projectManagerFactory);
            FindSquatsTool.Options options = new()
            {
                Quiet = true,
                Targets = new string[] { packageUrl }
            };
            (string output, int numSquats) result = await fst.RunAsync(options);
            Assert.IsTrue(expectedToHaveSquats ? result.numSquats > 0 : result.numSquats == 0);
        }

        [DataTestMethod]
        [DataRow("pkg:npm/angular/core", "pkg:npm/engular/core", "pkg:npm/angullar/core", "pkg:npm/core")]
        [DataRow("pkg:npm/%40angular/core", "pkg:npm/%40engular/core", "pkg:npm/%40angullar/core", "pkg:npm/core")] // back compat check
        [DataRow("pkg:npm/lodash", "pkg:npm/odash", "pkg:npm/lodah")]
        [DataRow("pkg:npm/babel/runtime", "pkg:npm/abel/runtime", "pkg:npm/bable/runtime", "pkg:npm/runtime")]
        public void ScopedNpmPackageSquats(string packageUrl, params string[] expectedSquats)
        {
            FindPackageSquats findPackageSquats =
                new(new ProjectManagerFactory(), new PackageURL(packageUrl));

            IDictionary<string, IList<Mutation>>? squatsCandidates = findPackageSquats.GenerateSquatCandidates();

            Assert.IsNotNull(squatsCandidates);

            foreach (string expectedSquat in expectedSquats)
            {
                Assert.IsTrue(squatsCandidates.ContainsKey(expectedSquat));
            }
        }

        [DataTestMethod]
        [DataRow("pkg:npm/foo", "pkg:npm/foojs")] // SuffixAdded, js
        [DataRow("pkg:npm/core-js", "pkg:npm/core-javascript")] // Substitution, js -> javascript
        [DataRow("pkg:npm/lodash", "pkg:npm/odash")] // RemovedCharacter, first character
        [DataRow("pkg:npm/angular/core", "pkg:npm/anguular/core")] // DoubleHit, third character
        [DataRow("pkg:npm/angular/core", "pkg:npm/core")] // RemovedNamespace, 'angular'
        [DataRow("pkg:nuget/Microsoft.CST.OAT", "pkg:nuget/microsoft.cst.oat.net")] // SuffixAdded, .net
        public void GenerateManagerSpecific(string packageUrl, string expectedToFind)
        {
            PackageURL purl = new(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                BaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
                if (manager is not null)
                {
                    foreach ((string _, IList<Mutation> mutations) in manager.EnumerateSquatCandidates(purl)!)
                    {
                        foreach (Mutation mutation in mutations)
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
        [DataRow("pkg:npm/angular/core", "pkg:npm/anngular/core")]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", "pkg:nuget/microsoft.cst.oat.net")]
        public void ConvertToAndFromJson(string packageUrl, string expectedToFind)
        {
            PackageURL purl = new(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                BaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
                if (manager is not null)
                {
                    foreach ((string mutationPurlString, IList<Mutation> mutations) in manager.EnumerateSquatCandidates(purl)!)
                    {
                        PackageURL mutationPurl = new(mutationPurlString);
                        FindPackageSquatResult result = new(mutationPurl.GetFullName(), mutationPurl,
                            purl, mutations);
                        string jsonResult = result.ToJson();
                        if (jsonResult.Contains(expectedToFind, StringComparison.OrdinalIgnoreCase))
                        {
                            FindPackageSquatResult fromJson = FindPackageSquatResult.FromJsonString(jsonResult);
                            if (fromJson.OriginalPackageUrl.ToString().Equals(purl.ToString(), StringComparison.OrdinalIgnoreCase)
                                && fromJson.MutatedPackageUrl.ToString().Equals(expectedToFind, StringComparison.OrdinalIgnoreCase))
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
        [DataRow("pkg:npm/foo/bar", typeof(UnicodeHomoglyphMutator))]
        [DataRow("pkg:nuget/Microsoft.CST.OAT", typeof(UnicodeHomoglyphMutator))]
        public void DontGenerateManagerSpecific(string packageUrl, Type notExpectedToFind)
        {
            PackageURL purl = new(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                BaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
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
        
        [DataTestMethod]
        [DataRow("pkg:npm/foo")]
        [DataRow("pkg:npm/foo/bar")]
        [DataRow("pkg:npm/react-dom")]
        [DataRow("pkg:nuget/Microsoft.CST.OAT")]
        [DataRow("pkg:nuget/Newtonsoft.Json")]
        public void EnsureHttpEncoded(string packageUrl)
        {
            PackageURL purl = new(packageUrl);
            if (purl.Name is not null && purl.Type is not null)
            {
                BaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
                if (manager is not null)
                {
                    foreach ((string mutationPurlString, _) in manager.EnumerateSquatCandidates(purl)!)
                    {
                        PackageURL mutatedPurl = new(mutationPurlString);
                        
                        if (IsUrlEncoded(mutatedPurl.Name) || (mutatedPurl.HasNamespace() && IsUrlEncoded(mutatedPurl.Namespace)))
                        {
                            return;
                        }
                    }
                }
            }
            Assert.Fail();
        }
        
        [TestMethod]
        public async Task LodashMutations_Succeeds_Async()
        {
            // arrange
            PackageURL lodash = new("pkg:npm/lodash@4.17.15");

            string[] squattingPackages = new[]
            {
                "pkg:npm/iodash", // ["AsciiHomoglyph","CloseLetters"]
                "pkg:npm/jodash", // ["AsciiHomoglyph"]
                "pkg:npm/1odash", // ["AsciiHomoglyph"]
                "pkg:npm/ledash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/ladash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/l0dash", // ["AsciiHomoglyph","CloseLetters"]
            };

            IHttpClientFactory httpClientFactory =
                FindSquatsHelper.SetupHttpCalls(purl: lodash, validSquats: squattingPackages);

            FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(httpClientFactory), lodash);

            // act
            IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
            List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
            Assert.IsNotNull(existingMutations);
            Assert.IsTrue(existingMutations.Any());
            string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
            CollectionAssert.AreEquivalent(squattingPackages, resultingMutationNames);
        }
        
        [TestMethod]
        public async Task LodashMutations_NoCache_Succeeds_Async()
        {
            // arrange
            PackageURL lodash = new("pkg:npm/lodash@4.17.15");

            string[] squattingPackages = new[]
            {
                "pkg:npm/iodash", // ["AsciiHomoglyph","CloseLetters"]
                "pkg:npm/jodash", // ["AsciiHomoglyph"]
                "pkg:npm/1odash", // ["AsciiHomoglyph"]
                "pkg:npm/ledash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/ladash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/l0dash", // ["AsciiHomoglyph","CloseLetters"]
            };

            IHttpClientFactory httpClientFactory =
                FindSquatsHelper.SetupHttpCalls(purl: lodash, validSquats: squattingPackages);

            FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(httpClientFactory), lodash);

            // act
            IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
            List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
            Assert.IsNotNull(existingMutations);
            Assert.IsTrue(existingMutations.Any());
            string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
            CollectionAssert.AreEquivalent(squattingPackages, resultingMutationNames);
        }
        
        [TestMethod]
        public async Task FooMutations_Succeeds_Async()
        {
            // arrange
            PackageURL foo = new("pkg:npm/foo");

            string[] squattingPackages = new[]
            {
                "pkg:npm/too", // ["AsciiHomoglyph", "CloseLetters"]
                "pkg:npm/goo", // ["CloseLetters"]
                "pkg:npm/foojs", // ["Suffix"]
                "pkg:npm/fooo", // ["DoubleHit"]
            };

            IHttpClientFactory httpClientFactory =
                FindSquatsHelper.SetupHttpCalls(purl: foo, validSquats: squattingPackages);

            FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(httpClientFactory), foo);

            // act
            IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
            List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
            Assert.IsNotNull(existingMutations);
            Assert.IsTrue(existingMutations.Any());
            string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
            CollectionAssert.AreEquivalent(squattingPackages, resultingMutationNames);
        }

        [TestMethod]
        public async Task NewtonsoftMutations_Succeeds_Async()
        {
            // arrange
            PackageURL newtonsoft = new("pkg:nuget/newtonsoft.json@12.0.2");

            string[] squattingPackages = new[]
            {
                "pkg:nuget/newtons0ft.json", // ["AsciiHomoglyph","CloseLetters"]
                "pkg:nuget/newtousoft.json", // ["AsciiHomoglyph"]
                "pkg:nuget/newtonsoft.jsan", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:nuget/mewtonsoft.json", // ["AsciiHomoglyph","BitFlip","CloseLetters"]
                "pkg:nuget/bewtonsoft.json", // ["CloseLetters"]
                "pkg:nuget/newtohsoft.json", // ["CloseLetters"]
            };

            IHttpClientFactory httpClientFactory =
                FindSquatsHelper.SetupHttpCalls(purl: newtonsoft, validSquats: squattingPackages);

            IManagerPackageActions<NuGetPackageVersionMetadata> packageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(newtonsoft, validSquats: squattingPackages) ?? throw new InvalidOperationException();
            Dictionary<string, ProjectManagerFactory.ConstructProjectManager> overrideDict = ProjectManagerFactory.GetDefaultManagers(httpClientFactory);

            overrideDict[NuGetProjectManager.Type] = directory =>
                new NuGetProjectManager(directory, packageActions, httpClientFactory);
            
            FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(overrideDict), newtonsoft);

            // act
            IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
            List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
            Assert.IsNotNull(existingMutations);
            Assert.IsTrue(existingMutations.Any());
            string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
            CollectionAssert.AreEquivalent(squattingPackages, resultingMutationNames);
        }

        [TestMethod]
        public async Task ScopedPackage_Succeeds_Async()
        {
            // arrange
            PackageURL angularCore = new PackageURL("pkg:npm/angular/core@13.0.0");

            string[] squattingPackages = new[]
            {
                "pkg:npm/anngular/core", // ["DoubleHit","Duplicator"]
                "pkg:npm/angullar/core", // ["DoubleHit","Duplicator"]
                "pkg:npm/angu1ar/core", //  ["AsciiHomoglyph"]
                "pkg:npm/anbular/core", // ["CloseLetters"]
                "pkg:npm/angula/core", // ["RemovedCharacter"]
                "pkg:npm/angularjs/core", // ["Suffix"]
                "pkg:npm/core", // ["RemovedNamespace"]
            };

            IHttpClientFactory httpClientFactory =
                FindSquatsHelper.SetupHttpCalls(purl: angularCore, validSquats: squattingPackages);

            FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(httpClientFactory), angularCore);

            // act
            IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
            List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
            Assert.IsNotNull(existingMutations);
            Assert.IsTrue(existingMutations.Any());
            string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
            CollectionAssert.AreEquivalent(squattingPackages, resultingMutationNames);
        }

        /// <summary>
        /// Helper method to check if a string is URL encoded.
        /// </summary>
        /// <param name="text">The string to check.</param>
        /// <returns>True if the string was URL encoded.</returns>
        private static bool IsUrlEncoded(string text)
        {
            return HttpUtility.UrlDecode(text) != text;
        }
    }
}
