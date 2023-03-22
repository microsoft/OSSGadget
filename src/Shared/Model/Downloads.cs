// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using Newtonsoft.Json;

public record Downloads()
{
    [JsonProperty(PropertyName = "daily", NullValueHandling = NullValueHandling.Ignore)]
    public long? Daily { get; set; }

    [JsonProperty(PropertyName = "monthly", NullValueHandling = NullValueHandling.Ignore)]
    public long? Monthly { get; set; }

    [JsonProperty(PropertyName = "overall", NullValueHandling = NullValueHandling.Ignore)]
    public long? Overall { get; set; }

    [JsonProperty(PropertyName = "weekly", NullValueHandling = NullValueHandling.Ignore)]
    public long? Weekly { get; set; }

    [JsonProperty(PropertyName = "yearly", NullValueHandling = NullValueHandling.Ignore)]
    public long? Yearly { get; set; }
}