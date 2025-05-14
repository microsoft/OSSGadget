// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using PackageManagers;
using PackageUrl;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class FindSourceTests
{
    [Theory]
    [InlineData("pkg:npm/md5", "https://github.com/pvorb/node-md5")]
    public async Task Check_Sarif(string purl, string targetResult)
    {
        // for initialization
        FindSourceTool tool = new();

        RepoSearch searchTool = new(new ProjectManagerFactory());
        Dictionary<PackageURL, double>? results = await searchTool.ResolvePackageLibraryAsync(new PackageURL(purl));

        List<Result> sarifResults = new();
        foreach (KeyValuePair<PackageURL, double> result in results)
        {
            double confidence = result.Value * 100.0;

            Result sarifResult = new()
            {
                Message = new Message()
                {
                    Text = $"https://github.com/{result.Key.Namespace}/{result.Key.Name}"
                },
                Kind = ResultKind.Informational,
                Level = FailureLevel.None,
                Rank = confidence,
                Locations = SarifOutputBuilder.BuildPurlLocation(new PackageURL(purl))
            };

            sarifResults.Add(sarifResult);
        }

        IOutputBuilder outputBuilder = OutputBuilderFactory.CreateOutputBuilder("sarifv2");
        outputBuilder.AppendOutput(sarifResults);
        string sarifJSON = outputBuilder.GetOutput();
        SarifLog? sarif = JsonConvert.DeserializeObject<SarifLog>(sarifJSON);
        Assert.NotNull(sarif);

        Run? sarifRun = sarif.Runs.FirstOrDefault();
        Assert.NotNull(sarifRun?.Tool.Driver.Name);

        // make sure atleast one of the result repos match the actual one
        bool found = false;
        if (sarifRun != null)
        {
            foreach (Result? result in sarifRun.Results)
            {
                if (result.Message.Text == targetResult)
                {
                    found = true;
                }
            }
        }
        Assert.True(found);
    }

    [Theory]
    [InlineData("pkg:npm/hjkfashfkjafhakfjsa", "")]
    [InlineData("pkg:pypi/hjkfashfkjafhakfjsa", "")]
    [InlineData("pkg:nuget/hjkfashfkjafhakfjsa", "")]
    public async Task FindSource_NonExistentPackage(string purl, string _)
    {
        // for initialization
        FindSourceTool tool = new();

        RepoSearch searchTool = new(new ProjectManagerFactory());
        Dictionary<PackageURL, double>? results = await searchTool.ResolvePackageLibraryAsync(new PackageURL(purl));
        Assert.True(results.Count() == 0, $"Result {results} obtained from non-existent {purl}");
    }

    [Theory]
    [InlineData("pkg:npm/md5", "pkg:github/pvorb/node-md5")]
    [InlineData("pkg:pypi/moment", "pkg:github/zachwill/moment")]
    [InlineData("pkg:nuget/Newtonsoft.Json", "pkg:github/jamesnk/newtonsoft.json")]
    [InlineData("pkg:pypi/django", "pkg:github/django/django")]
    [InlineData("pkg:pypi/pylint", "pkg:github/pylint-dev/pylint")]
    [InlineData("pkg:pypi/arrow", "pkg:github/arrow-py/arrow")]
    public async Task FindSource_Success(string purl, string targetResult)
    {
        // for initialization
        FindSourceTool tool = new();

        RepoSearch searchTool = new(new ProjectManagerFactory());
        Dictionary<PackageURL, double>? results = await searchTool.ResolvePackageLibraryAsync(new PackageURL(purl));
        PackageURL? targetPurl = new(targetResult);
        bool success = false;

        foreach (KeyValuePair<PackageURL, double> resultEntry in results)
        {
            if (resultEntry.Key.ToString().Equals(targetPurl.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                success = true;
            }
        }
        Assert.True(success, $"Result {targetResult} not found from {purl}");
    }
}