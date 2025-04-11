// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;

namespace Microsoft.CST.OpenSource.Model.Enums;

/// <summary>
/// Maven upstream repositories supported by Terrapin.
/// </summary>
public enum MavenSupportedUpstream
{
    /// <summary>
    /// https://repo1.maven.org/maven2
    /// </summary>
    MavenCentralRepository = 0,

    /// <summary>
    /// https://dl.google.com/android/maven2
    /// </summary>
    GoogleMavenRepository,
}

/// <summary>
/// Extension methods for <see cref="MavenSupportedUpstream"/>.
/// </summary>
public static class MavenSupportedUpstreamExtensions
{
    /// <summary>
    /// Gets the registry URI for the maven upstream repository.
    /// </summary>
    /// <param name="mavenUpstream">The <see cref="MavenSupportedUpstream"/>.</param>
    public static string GetRepositoryUrl(this MavenSupportedUpstream mavenUpstream) => mavenUpstream switch
    {
        MavenSupportedUpstream.MavenCentralRepository => "https://repo1.maven.org/maven2",
        MavenSupportedUpstream.GoogleMavenRepository => "https://maven.google.com/web/index.html#",
        _ => throw new ArgumentOutOfRangeException(nameof(mavenUpstream), mavenUpstream, null),
    };

    /// <summary>
    /// Returns the <see cref="MavenSupportedUpstream"/> associated with a given string.
    /// </summary>
    /// <param name="mavenUpstream"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static MavenSupportedUpstream GetMavenSupportedUpstream(this string mavenUpstream) => mavenUpstream switch
    {
        "MavenCentralRepository" => MavenSupportedUpstream.MavenCentralRepository,
        "https://repo1.maven.org/maven2" => MavenSupportedUpstream.MavenCentralRepository,
        "GoogleMavenRepository" => MavenSupportedUpstream.GoogleMavenRepository,
        "https://dl.google.com/android/maven2" => MavenSupportedUpstream.GoogleMavenRepository,
        _ => throw new ArgumentOutOfRangeException(nameof(mavenUpstream), mavenUpstream, null),
    };
}