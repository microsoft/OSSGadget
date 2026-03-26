// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

using Extensions;
using PackageUrl;
public class PackageUrlExtensionsTests
{
    [Theory]
    [InlineData("pkg:npm/lodash@4.17.15", "pkg-npm-lodash@4.17.15")]
    [InlineData("pkg:nuget/newtonsoft.json", "pkg-nuget-newtonsoft.json")]
    [InlineData("pkg:nuget/PSReadLine@2.2.0?repository_url=https://www.powershellgallery.com/api/v2", "pkg-nuget-PSReadLine@2.2.0")]
    public void ToStringFilenameSucceeds(string packageUrlString, string filename)
    {
        PackageURL packageUrl = new(packageUrlString);
        Assert.Equal(filename, packageUrl.ToStringFilename());
    }
}
