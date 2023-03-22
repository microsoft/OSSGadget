// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

using Newtonsoft.Json;

public record Digest()
{
    // TODO: Change to use an enum?
    [JsonProperty(PropertyName = "signature_algorithm", NullValueHandling = NullValueHandling.Ignore)]
    public string? Algorithm { get; set; }

    [JsonProperty(PropertyName = "signature", NullValueHandling = NullValueHandling.Ignore)]
    public string? Signature { get; set; }
}