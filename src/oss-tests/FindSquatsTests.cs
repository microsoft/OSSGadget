﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Contracts;
using Extensions;
using Helpers;
using Model.Metadata;
using Microsoft.CST.OpenSource.FindSquats;
using Microsoft.CST.OpenSource.FindSquats.ExtensionMethods;
using Microsoft.CST.OpenSource.FindSquats.Mutators;
using Microsoft.CST.OpenSource.PackageManagers;
using OssGadget.Options;
using PackageUrl;
using System.Linq;
using System.Net.Http;
using System.Web;

public class FindSquatsTest
{
    [Theory]
    [InlineData("pkg:nuget/Microsoft.CST.OAT", false, false)]
    [InlineData("pkg:npm/microsoft/microsoft-graph-library", false, false)]
    [InlineData("pkg:npm/foo", true, true)]
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
        managerOverrides[BaseNuGetProjectManager.Type] =
            (destinationDirectory, timeout, packageUrl) => new NuGetProjectManager(".", nugetPackageActions, httpClientFactory);
        
        ProjectManagerFactory projectManagerFactory = new(managerOverrides);
        FindSquatsTool fst = new(projectManagerFactory);
        FindSquatsToolOptions options = new()
        {
            Quiet = true,
            Targets = new string[] { packageUrl }
        };
        (string output, int numSquats) result = await fst.LegacyRunAsync(options);
        Assert.True(expectedToHaveSquats ? result.numSquats > 0 : result.numSquats == 0);
    }

    [Theory]
    [InlineData("pkg:npm/angular/core", "pkg:npm/engular/core", "pkg:npm/angullar/core", "pkg:npm/node-angular/core", "pkg:npm/core", "pkg:npm/angular-core", "pkg:npm/angular.core", "pkg:npm/angularcore")]
    [InlineData("pkg:npm/%40angular/core", "pkg:npm/%40engular/core", "pkg:npm/%40angullar/core", "pkg:npm/%40node-angular/core", "pkg:npm/core","pkg:npm/angular-core", "pkg:npm/angular.core", "pkg:npm/angularcore")] // back compat check
    [InlineData("pkg:npm/lodash", "pkg:npm/odash", "pkg:npm/lodah")]
    [InlineData("pkg:npm/babel/runtime", "pkg:npm/abel/runtime", "pkg:npm/bable/runtime", "pkg:npm/runtime")]
    public void ScopedNpmPackageSquats(string packageUrl, params string[] expectedSquats)
    {
        FindPackageSquats findPackageSquats =
            new(new ProjectManagerFactory(), new PackageURL(packageUrl));

        IDictionary<string, IList<Mutation>>? squatsCandidates = findPackageSquats.GenerateSquatCandidates();

        Assert.NotNull(squatsCandidates);

        foreach (string expectedSquat in expectedSquats)
        {
            Assert.True(squatsCandidates.ContainsKey(expectedSquat));
        }
    }

    [Theory]
    [InlineData("pkg:npm/foo", "pkg:npm/foojs")] // SuffixAdded, js
    [InlineData("pkg:npm/core-js", "pkg:npm/core-javascript")] // Substitution, js -> javascript
    [InlineData("pkg:npm/lodash", "pkg:npm/odash")] // RemovedCharacter, first character
    [InlineData("pkg:npm/angular/core", "pkg:npm/anguular/core")] // DoubleHit, third character
    [InlineData("pkg:npm/angular/core", "pkg:npm/core")] // RemovedNamespace, 'angular'
    [InlineData("pkg:npm/angular/core", "pkg:npm/angular-core")] // NamespaceInName, 'angular'
    [InlineData("pkg:npm/angular/core", "pkg:npm/angularcore")] // NamespaceInName, 'angular'
    [InlineData("pkg:npm/%40angular/core", "pkg:npm/angular.core")] // NamespaceInName, 'angular'
    [InlineData("pkg:nuget/Microsoft.CST.OAT", "pkg:nuget/microsoft.cst.oat.net")] // SuffixAdded, .net

    // Based on attack seen here: https://medium.com/checkmarx-security/new-attack-vector-observed-targeting-net-developers-in-a-software-supply-chain-attack-c28bfe4decd2
    [InlineData("pkg:nuget/Coinbase", "pkg:nuget/Coinbase.Core")] // SuffixAdded, .core
    public void GenerateManagerSpecific(string packageUrl, string expectedToFind)
    {
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
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
    
    [Theory]
    [InlineData("pkg:npm/angular/core")]
    [InlineData("pkg:npm/%40angular/core")]
    public void ExcludeRemoveNamespaceMutator(string packageUrl)
    {
        MutateOptions mutateOptions = new()
        {
            ExcludedMutators = new[] { MutatorType.RemovedNamespace }
        };

        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
            if (manager is not null)
            {
                foreach ((string _, IList<Mutation> mutations) in manager.EnumerateSquatCandidates(purl, options: mutateOptions)!)
                {
                    if (mutations.All(m => m.Mutator != MutatorType.RemovedNamespace))
                    {
                        return;
                    }
                }
            }
        }
        Assert.Fail($"Found a mutation with a removed namespace, which shouldn't happen");
    }

    [Theory]
    [InlineData("pkg:npm/angular/core")]
    [InlineData("pkg:npm/%40angular/core")]
    [InlineData("pkg:npm/foo")]
    [InlineData("pkg:npm/rx")]
    [InlineData("pkg:npm/q")]
    [InlineData("pkg:npm/typescript")]
    [InlineData("pkg:npm/react")]
    [InlineData("pkg:npm/express")]
    [InlineData("pkg:npm/core-js")]
    [InlineData("pkg:npm/lodash")]
    [InlineData("pkg:pypi/pandas")]
    [InlineData("pkg:pypi/python-dateutil")]
    [InlineData("pkg:pypi/google-api-python-client")]
    [InlineData("pkg:nuget/Microsoft.CST.OAT")]
    public void OriginalIsFullName(string packageUrl)
    {
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
            if (manager is not null)
            {
                foreach ((string _, IList<Mutation> mutations) in manager.EnumerateSquatCandidates(purl)!)
                {
                    if (mutations.All(m => m.Original == packageUrl))
                    {
                        return;
                    }
                }
            }
        }
        Assert.Fail($"Found a mutation where the Mutator.Original didn't match the original purl.");
    }
    
    [Theory]
    [InlineData("pkg:npm/i")]
    [InlineData("pkg:npm/ts")]
    [InlineData("pkg:nuget/d")]
    [InlineData("pkg:pypi/python")]
    public void DontMakeMutationsOfJustSeparators(string packageUrl)
    {
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
            if (manager is not null)
            {
                foreach ((string _, IList<Mutation> mutations) in manager.EnumerateSquatCandidates(purl)!)
                {
                    if (mutations.Any(m =>
                        {
                            var mutatedPurl = new PackageURL(m.Mutated);
                            return mutatedPurl.Name.Length == 1 &&
                                   !char.IsLetterOrDigit(mutatedPurl.Name[0]);
                        }))
                    {
                        Assert.Fail($"Found a mutation that's a separator.");
                    }
                }
            }
        }
    }
    
    [Theory]
    [InlineData("pkg:npm/i")]
    [InlineData("pkg:npm/ts")]
    [InlineData("pkg:nuget/d")]
    [InlineData("pkg:pypi/python")]
    public void DontMakeEmptyMutations(string packageUrl)
    {
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
            if (manager is not null)
            {
                foreach ((string mutation, IList<Mutation> mutations) in manager.EnumerateSquatCandidates(purl)!)
                {
                    try
                    {
                        var mutatedPurl = new PackageURL(mutation);
                        if (mutations.Any(m => mutatedPurl.Name.Length == 0))
                        {
                            Assert.Fail($"Found a mutation that's got an empty name.");
                        }
                    }
                    catch (MalformedPackageUrlException e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }

                }
            }
        }
    }
    
    [Theory]
    [InlineData("pkg:npm/foo")]
    [InlineData("pkg:npm/rx")]
    [InlineData("pkg:npm/q")]
    [InlineData("pkg:npm/typescript")]
    [InlineData("pkg:npm/react")]
    [InlineData("pkg:npm/express")]
    [InlineData("pkg:npm/core-js")]
    [InlineData("pkg:npm/lodash")]
    [InlineData("pkg:npm/%40angular/core")]
    [InlineData("pkg:pypi/pandas")]
    [InlineData("pkg:pypi/python-dateutil")]
    [InlineData("pkg:pypi/google-api-python-client")]
    [InlineData("pkg:nuget/Microsoft.CST.OAT")]
    public void CanGetOriginalFromMutatingMutations(string packageUrl)
    {
        var missedOnes = new List<Mutation>();
        var originalMutationCount = 0;
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
            if (manager is not null)
            {
                var originalMutations = manager.EnumerateSquatCandidates(purl);
                originalMutationCount = originalMutations.Count;
                foreach ((string mutatedNameFromOriginal, IList<Mutation> mutations) in originalMutations!)
                {
                    // Don't want to consider bit flips, too complicated to reverse.
                    if (mutations.All(m => m.Mutator == MutatorType.BitFlip))
                    {
                        continue;
                    }
                    
                    // Don't want to consider separated section removed, separator removed, removed namespace, or namespace in name, too complicated to reverse.
                    if (mutations.Any(m => m.Mutator is MutatorType.RemoveSeparatedSection or MutatorType.SeparatorRemoved or MutatorType.RemovedNamespace or MutatorType.NamespaceInName))
                    {
                        continue;
                    }

                    try
                    {
                        var mutatedMutations =
                            manager.EnumerateSquatCandidates(new PackageURL(mutatedNameFromOriginal));

                        if (!mutatedMutations.Any(
                                (kvp) => new PackageURL(kvp.Key).ToString().Equals(packageUrl, StringComparison.OrdinalIgnoreCase)))
                        {
                            missedOnes.AddRange(mutations);
                        }
                    }
                    catch (MalformedPackageUrlException)
                    {
                        continue;
                    }
                }
            }
        }

        if (missedOnes.Any())
        {
            var missed = missedOnes.Select(m => $"Mutated name: {m.Mutated} - {m.Reason}");
            var message = $"Found {missed.Count()} mutations out of {originalMutationCount} that couldn't be reverted back to {packageUrl}:\n{string.Join("\n", missed)}";
            Assert.Fail(message);
        }
    }

    [Theory]
    [InlineData("pkg:npm/foo", "pkg:npm/too")]
    [InlineData("pkg:npm/angular/core", "pkg:npm/anngular/core")]
    [InlineData("pkg:nuget/Microsoft.CST.OAT", "pkg:nuget/microsoft.cst.oat.net")]
    public void ConvertToAndFromJson(string packageUrl, string expectedToFind)
    {
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
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

    [Theory]
    [InlineData("pkg:npm/foo", typeof(UnicodeHomoglyphMutator))]
    [InlineData("pkg:npm/foo/bar", typeof(UnicodeHomoglyphMutator))]
    [InlineData("pkg:nuget/Microsoft.CST.OAT", typeof(UnicodeHomoglyphMutator))]
    public void DontGenerateManagerSpecific(string packageUrl, Type notExpectedToFind)
    {
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
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
    
    [Theory]
    [InlineData("pkg:npm/%40foo/bar")]
    public void EnsureHttpEncoded(string packageUrl)
    {
        PackageURL purl = new(packageUrl);
        if (purl.Name is not null && purl.Type is not null)
        {
            IBaseProjectManager? manager = ProjectManagerFactory.ConstructPackageManager(purl, null);
            if (manager is not null)
            {
                foreach ((string mutationPurlString, _) in manager.EnumerateSquatCandidates(purl)!)
                {
                    if (IsUrlEncoded(mutationPurlString))
                    {
                        return;
                    }
                }
            }
        }
        Assert.Fail();
    }
    
    [Fact]
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
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
    }
    
    [Fact]
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
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
    }
    
    [Fact]
    public async Task UniversalifyMutations_Succeeds_Async()
    {
        // arrange
        PackageURL universalify = new("pkg:npm/universalify");

        string[] squattingPackages = new[]
        {
            "pkg:npm/universalfiy", 
            "pkg:npm/nuiversalify",
            "pkg:npm/universalifyt",
        };

        IHttpClientFactory httpClientFactory =
            FindSquatsHelper.SetupHttpCalls(purl: universalify, validSquats: squattingPackages);

        FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(httpClientFactory), universalify);

        // act
        IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
        List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
    }
    
    [Fact]
    public async Task WebidlConversionsMutations_Succeeds_Async()
    {
        // arrange
        PackageURL webidlConversions = new("pkg:npm/webidl-conversions");

        string[] squattingPackages = new[]
        {
            "pkg:npm/webidl-conversion", 
            "pkg:npm/webidl-covnersions",
            "pkg:npm/ewbidl-conversions",
        };

        IHttpClientFactory httpClientFactory =
            FindSquatsHelper.SetupHttpCalls(purl: webidlConversions, validSquats: squattingPackages);

        FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(httpClientFactory), webidlConversions);

        // act
        IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
        List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
    }

    [Fact]
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
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
    }

    [Fact]
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

        overrideDict[BaseNuGetProjectManager.Type] = (destinationDirectory, timeout, packageUrl) =>
            new NuGetProjectManager(destinationDirectory, packageActions, httpClientFactory);
        
        FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(overrideDict), newtonsoft);

        // act
        IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
        List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
    }

    [Fact]
    public async Task RequestsMutations_Succeeds_Async()
    {
        // arrange
        PackageURL requests = new("pkg:pypi/requests@2.28.2");

        string[] squattingPackages = new[]
        {
            "pkg:pypi/reuquests", // AddContainingCharacter
            "pkg:pypi/requestss", // DoubleHit
            "pkg:pypi/reqests", // RemovedCharacter
            "pkg:pypi/request", // RemovedCharacter
            "pkg:pypi/requets", // RemovedCharacter
        };

        IHttpClientFactory httpClientFactory =
            FindSquatsHelper.SetupHttpCalls(purl: requests, validSquats: squattingPackages);

        IManagerPackageActions<NuGetPackageVersionMetadata> packageActions = PackageActionsHelper<NuGetPackageVersionMetadata>.SetupPackageActions(requests, validSquats: squattingPackages) ?? throw new InvalidOperationException();
        Dictionary<string, ProjectManagerFactory.ConstructProjectManager> overrideDict = ProjectManagerFactory.GetDefaultManagers(httpClientFactory);

        overrideDict[BaseNuGetProjectManager.Type] = (destinationDirectory, timeout, packageUrl) =>
            new NuGetProjectManager(destinationDirectory, packageActions, httpClientFactory);
        
        FindPackageSquats findPackageSquats = new(new ProjectManagerFactory(overrideDict), requests);

        // act
        IDictionary<string, IList<Mutation>>? squatCandidates = findPackageSquats.GenerateSquatCandidates();
        List<FindPackageSquatResult> existingMutations = await findPackageSquats.FindExistingSquatsAsync(squatCandidates, new MutateOptions(){UseCache = false}).ToListAsync();
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
    }
    
    [Fact]
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
        Assert.NotNull(existingMutations);
        Assert.True(existingMutations.Any());
        string[] resultingMutationNames = existingMutations.Select(m => m.MutatedPackageUrl.ToString()).ToArray();
        squattingPackages.Should().BeEquivalentTo(resultingMutationNames);
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
