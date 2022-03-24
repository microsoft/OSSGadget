// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Metadata;

using Contracts;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

/// <summary>
/// A class to represent Package Metadata for a NuGet package.
/// </summary>
public record NuGetMetadata : IManagerMetadata
{
    [JsonProperty(PropertyName = JsonProperties.Authors)]
    [JsonConverter(typeof(MetadataFieldConverter))]
    public string Authors { get; init; }

    [JsonProperty(PropertyName = JsonProperties.DependencyGroups, ItemConverterType = typeof(PackageDependencyGroupConverter))]
    public IEnumerable<PackageDependencyGroup> DependencySetsInternal { get; init; }

    [JsonIgnore]
    public IEnumerable<PackageDependencyGroup> DependencySets => DependencySetsInternal;

    [JsonProperty(PropertyName = JsonProperties.Description)]
    public string Description { get; init; }

    [JsonProperty(PropertyName = JsonProperties.DownloadCount)]
    public long? DownloadCount { get; init; }

    [JsonProperty(PropertyName = JsonProperties.IconUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri IconUrl { get; init; }

    [JsonIgnore]
    public PackageIdentity Identity => new(Name, new NuGetVersion(Version));

    [JsonProperty(PropertyName = JsonProperties.Version)]
    public string Version { get; init; }

    [JsonProperty(PropertyName = JsonProperties.LicenseUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri LicenseUrl { get; init; }

    [JsonProperty(PropertyName = JsonProperties.ProjectUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri ProjectUrl { get; init; }

    [JsonProperty(PropertyName = JsonProperties.ReadmeUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri ReadmeUrl { get; init; }

    [JsonIgnore]
    public Uri ReportAbuseUrl { get; }

    [JsonProperty(PropertyName = "packageDetailsUrl")]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri PackageDetailsUrl { get; init; }

    [JsonProperty(PropertyName = JsonProperties.Published)]
    public DateTimeOffset? Published { get; init; }

    [JsonProperty(PropertyName = JsonProperties.Owners)]
    [JsonConverter(typeof(MetadataFieldConverter))]
    public string Owners { get; init; }

    [JsonProperty(PropertyName = JsonProperties.PackageId)]
    public string Name { get; init; }

    [JsonProperty(PropertyName = JsonProperties.RequireLicenseAcceptance, DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(false)]
    [JsonConverter(typeof(SafeBoolConverter))]
    public bool RequireLicenseAcceptance { get; init; }

    [JsonProperty(PropertyName = JsonProperties.Summary)]
    public string Summary { get; init; }

    [JsonProperty(PropertyName = JsonProperties.Tags)]
    [JsonConverter(typeof(MetadataFieldConverter))]
    public string Tags { get; init; }

    [JsonProperty(PropertyName = JsonProperties.Title)]
    public string Title { get; init; }

    [JsonProperty(PropertyName = JsonProperties.Listed)]
    public bool IsListed { get; init; }

    [JsonProperty(PropertyName = JsonProperties.PrefixReserved)]
    public bool PrefixReserved { get; init; }

    [JsonIgnore]
    public LicenseMetadata LicenseMetadata { get; }

    [JsonProperty(PropertyName = JsonProperties.Vulnerabilities)]
    public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; init; }

    [JsonProperty(PropertyName = JsonProperties.SubjectId)]
    public Uri CatalogUri { get; init; }

    /// <summary>
    /// Initialize an instance of <see cref="NuGetMetadata"/> using the <see cref="JsonConstructorAttribute"/>.
    /// </summary>
    /// <remarks>Necessary for unit test implementation.</remarks>
    [JsonConstructor]
#pragma warning disable CS8618
    public NuGetMetadata()
#pragma warning restore CS8618
    {}

    /// <summary>
    /// Initialize an instance of <see cref="NuGetMetadata"/> using values from a <see cref="PackageSearchMetadataRegistration"/>.
    /// </summary>
    /// <param name="registration">The <see cref="PackageSearchMetadataRegistration"/> to get the values from.</param>
    public NuGetMetadata(PackageSearchMetadataRegistration registration)
    {
        Authors = registration.Authors;
        DependencySetsInternal = registration.DependencySets;
        Description = registration.Description;
        DownloadCount = registration.DownloadCount;
        IconUrl = registration.IconUrl;
        Name = registration.PackageId;
        Version = registration.Version.ToString();
        LicenseUrl = registration.LicenseUrl;
        ProjectUrl = registration.ProjectUrl;
        ReadmeUrl = registration.ReadmeUrl;
        ReportAbuseUrl = registration.ReportAbuseUrl;
        PackageDetailsUrl = registration.PackageDetailsUrl;
        Published = registration.Published;
        Owners = registration.Owners;
        RequireLicenseAcceptance = registration.RequireLicenseAcceptance;
        Summary = registration.Summary;
        Tags = registration.Tags;
        Title = registration.Title;
        IsListed = registration.IsListed;
        PrefixReserved = registration.PrefixReserved;
        LicenseMetadata = registration.LicenseMetadata;
        Vulnerabilities = registration.Vulnerabilities;
        CatalogUri = registration.CatalogUri;
    }

    /// <summary>
    /// Serialize this instance of <see cref="NuGetMetadata"/> into a json string.
    /// </summary>
    /// <returns>A json string representing this object.</returns>
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this);
    }

    /// <summary>
    /// Deserialize a json string into a <see cref="NuGetMetadata"/> object.
    /// </summary>
    /// <param name="json">The json string representing a <see cref="NuGetMetadata"/>.</param>
    /// <returns>The <see cref="NuGetMetadata"/> constructed from json.</returns>
    public static NuGetMetadata? FromJson(string json)
    {
        return JsonConvert.DeserializeObject<NuGetMetadata>(json);
    }
} 