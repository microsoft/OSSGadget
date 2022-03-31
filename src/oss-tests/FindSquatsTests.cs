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
    using Extensions;
    using Moq;
    using PackageUrl;
    using RichardSzalay.MockHttp;
    using System.Linq;
    using System.Net;
    using System.Net.Http;

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
        [DataRow("pkg:npm/angular/core", "pkg:npm/engular/core", "pkg:npm/angullar/core", "pkg:npm/core")]
        [DataRow("pkg:npm/%40angular/core", "pkg:npm/%40engular/core", "pkg:npm/%40angullar/core", "pkg:npm/core")] // back compat check
        [DataRow("pkg:npm/lodash", "pkg:npm/odash", "pkg:npm/lodah")]
        [DataRow("pkg:npm/babel/runtime", "pkg:npm/abel/runtime", "pkg:npm/bable/runtime", "pkg:npm/runtime")]
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
                BaseProjectManager? manager = ProjectManagerFactory.CreateProjectManager(purl, null);
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
                BaseProjectManager? manager = ProjectManagerFactory.CreateProjectManager(purl, null);
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
        
        [TestMethod]
        public async Task LodashMutations_Succeeds_Async()
        {
            // arrange
            PackageURL lodash = new("pkg:npm/lodash@4.17.15");
            string lodashUrl = GetRegistryUrl(lodash);

            string[] squattingPackages = new[]
            {
                "pkg:npm/iodash", // ["AsciiHomoglyph","CloseLetters"]
                "pkg:npm/jodash", // ["AsciiHomoglyph"]
                "pkg:npm/1odash", // ["AsciiHomoglyph"]
                "pkg:npm/ledash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/ladash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/l0dash", // ["AsciiHomoglyph","CloseLetters"]
            };

            Mock<IHttpClientFactory> mockFactory = new();

            using MockHttpMessageHandler httpMock = new();
            MockHttpFetchResponse(HttpStatusCode.OK, lodashUrl, httpMock);

            MockSquattedPackages(httpMock, squattingPackages);

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpMock.ToHttpClient());

            FindPackageSquats findPackageSquats = new(mockFactory.Object, lodash);

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
            string lodashUrl = GetRegistryUrl(lodash);

            string[] squattingPackages = new[]
            {
                "pkg:npm/iodash", // ["AsciiHomoglyph","CloseLetters"]
                "pkg:npm/jodash", // ["AsciiHomoglyph"]
                "pkg:npm/1odash", // ["AsciiHomoglyph"]
                "pkg:npm/ledash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/ladash", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:npm/l0dash", // ["AsciiHomoglyph","CloseLetters"]
            };

            Mock<IHttpClientFactory> mockFactory = new();

            using MockHttpMessageHandler httpMock = new();
            MockHttpFetchResponse(HttpStatusCode.OK, lodashUrl, httpMock);

            MockSquattedPackages(httpMock, squattingPackages);

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpMock.ToHttpClient());

            FindPackageSquats findPackageSquats = new(mockFactory.Object, lodash);

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
            string fooUrl = GetRegistryUrl(foo);

            string[] squattingPackages = new[]
            {
                "pkg:npm/too", // ["AsciiHomoglyph", "CloseLetters"]
                "pkg:npm/goo", // ["CloseLetters"]
                "pkg:npm/foojs", // ["Suffix"]
                "pkg:npm/fooo", // ["DoubleHit"]
            };

            Mock<IHttpClientFactory> mockFactory = new();

            using MockHttpMessageHandler httpMock = new();
            MockHttpFetchResponse(HttpStatusCode.OK, fooUrl, httpMock);

            MockSquattedPackages(httpMock, squattingPackages);

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpMock.ToHttpClient());

            FindPackageSquats findPackageSquats = new(mockFactory.Object, foo);

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
            string newtonsoftUrl = GetRegistryUrl(newtonsoft);

            string[] squattingPackages = new[]
            {
                "pkg:nuget/newtons0ft.json", // ["AsciiHomoglyph","CloseLetters"]
                "pkg:nuget/newtousoft.json", // ["AsciiHomoglyph"]
                "pkg:nuget/newtonsoft.jsan", // ["AsciiHomoglyph","VowelSwap"]
                "pkg:nuget/mewtonsoft.json", // ["AsciiHomoglyph","BitFlip","CloseLetters"]
                "pkg:nuget/bewtonsoft.json", // ["CloseLetters"]
                "pkg:nuget/newtohsoft.json", // ["CloseLetters"]
            };

            Mock<IHttpClientFactory> mockFactory = new();

            using MockHttpMessageHandler httpMock = new();
            MockHttpFetchResponse(HttpStatusCode.OK, newtonsoftUrl, httpMock);

            MockSquattedPackages(httpMock, squattingPackages);

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpMock.ToHttpClient());

            FindPackageSquats findPackageSquats = new(mockFactory.Object, newtonsoft);

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
            string angularCoreUrl = GetRegistryUrl(angularCore);

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

            Mock<IHttpClientFactory> mockFactory = new();

            using MockHttpMessageHandler httpMock = new();
            MockHttpFetchResponse(HttpStatusCode.OK, angularCoreUrl, httpMock);

            MockSquattedPackages(httpMock, squattingPackages);

            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpMock.ToHttpClient());

            FindPackageSquats findPackageSquats = new(mockFactory.Object, angularCore);

            // act
            IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
            List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
            Assert.IsNotNull(existingMutations);
            Assert.IsTrue(existingMutations.Any());
            string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
            CollectionAssert.AreEquivalent(squattingPackages, resultingMutationNames);
        }
        
        private static void MockHttpFetchResponse(
            HttpStatusCode statusCode,
            string url,
            MockHttpMessageHandler httpMock)
        {
            httpMock
                .When(HttpMethod.Get, url)
                .Respond(statusCode, "application/json", "{}");
        }
        
        private static void MockSquattedPackages(MockHttpMessageHandler httpMock, string[] squattedPurls)
        {
            foreach (PackageURL mutatedPackage in squattedPurls.Select(mutatedPurl => new PackageURL(mutatedPurl)))
            {
                string url = GetRegistryUrl(mutatedPackage);
                MockHttpFetchResponse(HttpStatusCode.OK, url, httpMock);
            }
        }
        
        private static string GetRegistryUrl(PackageURL purl)
        {
            return purl.Type switch
            {
                "npm" => $"{NPMProjectManager.ENV_NPM_API_ENDPOINT}/{purl.GetFullName()}",
                "nuget" => $"{NuGetProjectManager.NUGET_DEFAULT_REGISTRATION_ENDPOINT}{purl.Name.ToLowerInvariant()}/index.json",
                "pypi" => $"{PyPIProjectManager.ENV_PYPI_ENDPOINT}/pypi/{purl.Name}/json",
                _ => throw new NotSupportedException(
                    $"{purl.Type} packages are not currently supported."),
            };
        }
    }
}
