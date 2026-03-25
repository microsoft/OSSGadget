// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.PackageManagers.CPAN;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class CPANVersionResponse
{
    [JsonPropertyName("took")]
    public int Took { get; set; }

    [JsonPropertyName("hits")]
    public CPANVersionHitCollection Hits { get; set; } = new();

    [JsonPropertyName("timed_out")]
    public bool TimedOut { get; set; }
}

public class CPANVersionHitCollection
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("hits")]
    public List<CPANVersionHit> Hits { get; set; } = new List<CPANVersionHit>();
}

public class CPANVersionHit
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("_index")]
    public string Index { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public CPANVersionFields Fields { get; set; } = new();

    [JsonPropertyName("_type")]
    public string Type { get; set; } = string.Empty;
}

public class CPANVersionFields
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}