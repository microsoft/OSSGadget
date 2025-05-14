// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Extensions;
using PackageUrl;
public class PackageUrlExtensionsTests
{
    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15", "pkg-npm-lodash@4.17.15")]
    [InlineData("pkg:nuget/newtonsoft.json", "pkg-nuget-newtonsoft.json")]
    public void ToStringFilenameSucceeds(string packageUrlString, string filename)
    {
        PackageURL packageUrl = new(packageUrlString);
        Assert.Equal(filename, packageUrl.ToStringFilename());
    }
}
