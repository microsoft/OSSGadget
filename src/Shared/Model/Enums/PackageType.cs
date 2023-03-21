// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Enums;

using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

// [JsonConverter(typeof(JsonStringEnumMemberConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PackageType
{
    [EnumMember(Value = "cargo")]
    Cargo,

    [EnumMember(Value = "cocoapods")]
    Cocoapods,

    [EnumMember(Value = "composer")]
    Composer,

    [EnumMember(Value = "conda")]
    Conda,

    [EnumMember(Value = "cpan")]
    CPAN,

    [EnumMember(Value = "cran")]
    CRAN,

    [EnumMember(Value = "gem")]
    Gem,

    [EnumMember(Value = "github")]
    GitHub,

    [EnumMember(Value = "golang")]
    Golang,

    [EnumMember(Value = "hackage")]
    Hackage,

    [EnumMember(Value = "maven")]
    Maven,

    [EnumMember(Value = "npm")]
    Npm,

    [EnumMember(Value = "nuget")]
    NuGet,

    [EnumMember(Value = "pypi")]
    PyPi,

    [EnumMember(Value = "ubuntu")]
    Ubuntu,

    [EnumMember(Value = "url")]
    URL,

    [EnumMember(Value = "vsm")]
    VSM,
}