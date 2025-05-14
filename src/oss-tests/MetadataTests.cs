// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using System.Threading.Tasks;
using DevLab.JmesPath;
using Microsoft.CST.OpenSource;
using PackageUrl;
using System.Text.Json;

public class MetadataTests
{
    [Theory]
    [InlineData("deps.dev", "pkg:npm/blake3", "package.name", "blake3")]
    [InlineData("deps.dev", "pkg:npm/blake3@3.0.0", "package.name", "blake3")]
    [InlineData("libraries.io", "pkg:npm/blake3@3.0.0", "name", "blake3")]
    public async Task Check_Metadata(string dataSource, string purlString, string jmesPathExpr, string targetResult)
    {
        BaseMetadataSource? metadataSource = null;
        if (string.Equals(dataSource, "deps.dev", StringComparison.InvariantCultureIgnoreCase))
            metadataSource = new DepsDevMetadataSource();
        else if (string.Equals(dataSource, "libraries.io", StringComparison.InvariantCultureIgnoreCase))
            metadataSource = new LibrariesIoMetadataSource();
        else
            Assert.Fail("Unknown data source: " + dataSource);

        PackageURL purl = new(purlString);
        JsonDocument? metadata = await metadataSource.GetMetadataForPackageUrlAsync(purl, false);
        if (metadata == null)
            Assert.Fail($"Unable to load metadata for package: {purlString}");
        
        var jmesPath = new JmesPath();
        var result = jmesPath.Transform(System.Text.Json.JsonSerializer.Serialize(metadata), jmesPathExpr);
        if (result == null)
            Assert.Fail($"Unable to evaluate JMESPath expression: {jmesPathExpr}");
        
        var resultString = result.ToString().Trim('"');
        Assert.Equal(targetResult, resultString);
    }
}