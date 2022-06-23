﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Threading.Tasks;
using DevLab.JmesPath;
using Microsoft.CST.OpenSource;
using PackageUrl;
using System;
using System.Text.Json;

[TestClass]
public class MetadataTests
{
    [DataTestMethod]
    [DataRow("deps.dev", "pkg:npm/left-pad", "package.name", "left-pad")]
    [DataRow("deps.dev", "pkg:npm/left-pad@1.3.0", "package.name", "left-pad")]
    [DataRow("libraries.io", "pkg:npm/left-pad", "name", "left-pad")]
    [DataRow("libraries.io", "pkg:npm/left-pad@1.3.0", "name", "left-pad")]
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
            Assert.Fail("Unable to load metadata for package: {}", purlString);
        
        var jmesPath = new JmesPath();
        var result = jmesPath.Transform(System.Text.Json.JsonSerializer.Serialize(metadata), jmesPathExpr);
        if (result == null)
            Assert.Fail("Unable to evaluate JMESPath expression: {}", jmesPathExpr);
        
        var resultString = result.ToString().Trim('"');
        Assert.AreEqual(targetResult, resultString);
    }
}