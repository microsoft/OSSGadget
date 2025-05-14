// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using OssGadget.Options;
using Microsoft.CST.OpenSource.Reproducibility;
using System.Text.Json;

public class ReproducibleTest
{
    [Theory]
    [InlineData("pkg:npm/left-pad@1.3.0", true)]
    [InlineData("pkg:npm/non-existent1267461827467@12421", false)]
    public async Task CheckReproducibility(string packageUrl, bool? expectedToBeReproducible)
    {
        string? outputFilename = Guid.NewGuid().ToString() + ".json";
        var options = new ReproducibleToolOptions()
        {
            AllStrategies = true, OutputFile = outputFilename, Targets = new[] { packageUrl }
        };
        await new ReproducibleTool().RunAsync(options);

        bool outputFileExists = File.Exists(outputFilename);

        if (expectedToBeReproducible != null)
        {
            Assert.True(outputFileExists, "Output file does not exist.");
            string? result = File.ReadAllText(outputFilename);

            List<ReproducibleToolResult>? jsonResults = JsonSerializer.Deserialize<List<ReproducibleToolResult>>(result);
            Assert.NotNull(jsonResults); // "Output file was not parseable."

            Assert.Equal(expectedToBeReproducible == true, jsonResults.First().IsReproducible);
        }
        else
        {
            if (outputFileExists)
            {
                File.Delete(outputFilename);
            }
            Assert.True(!outputFileExists, "File was produced but should have been an error.");
        }

        // Cleanup
        if (File.Exists(outputFilename))
        {
            File.Delete(outputFilename);
        }

    }


    [Theory]
    [InlineData("/foo/bar/quux.c", "quux.c", "quux.c")]
    [InlineData("/foo/bar/quux.c", "baz.c", null)]
    [InlineData("/foo/bar/quux.c", "baz/quux.c,bar/quux.c", "bar/quux.c")]
    public async Task CheckGetClosestMatch(string filename, string targets, string expectedTarget)
    {
        IEnumerable<string>? results = OssReproducibleHelpers.GetClosestFileMatch(filename, targets.Split(','));
        Assert.NotNull(results);

        if (expectedTarget == null)
        {
            Assert.False(results.Any());
        }
        else
        {
            Assert.True(results.Any());
            Assert.Equal(expectedTarget, results.First());
        }
    }
}
