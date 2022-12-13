// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Metadata.NPM;

using System.Text.Json.Serialization;

public class NPMDownloads
{
    [JsonPropertyName("start")]
    public string Start { get; set; }
    
    [JsonPropertyName("end")]
    public string End { get; set; }
    
    [JsonPropertyName("package")]
    public string Package { get; set; }
    
    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }
}