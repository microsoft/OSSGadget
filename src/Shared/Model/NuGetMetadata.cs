// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model;

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
public class NuGetMetadata : IPackageSearchMetadata
{
    public Task<PackageDeprecationMetadata> GetDeprecationMetadataAsync() => throw new NotImplementedException();

    public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => throw new NotImplementedException();

    [JsonProperty(PropertyName = JsonProperties.Authors)]
    [JsonConverter(typeof(MetadataFieldConverter))]
    public string Authors { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.DependencyGroups, ItemConverterType = typeof(PackageDependencyGroupConverter))]
    public IEnumerable<PackageDependencyGroup> DependencySetsInternal { get; private set; }

    [JsonIgnore]
    public IEnumerable<PackageDependencyGroup> DependencySets => DependencySetsInternal;
    
    [JsonProperty(PropertyName = JsonProperties.Description)]
    public string Description { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.DownloadCount)]
    public long? DownloadCount { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.IconUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri IconUrl { get; private set; }
    
    [JsonIgnore]
    public PackageIdentity Identity => new(PackageId, new NuGetVersion(Version));
    
    [JsonProperty(PropertyName = JsonProperties.Version)]
    public string Version { get; private set; }

    [JsonProperty(PropertyName = JsonProperties.LicenseUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri LicenseUrl { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.ProjectUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri ProjectUrl { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.ReadmeUrl)]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri ReadmeUrl { get; private set; }
    
    [JsonIgnore]
    public Uri ReportAbuseUrl { get; }
    
    [JsonProperty(PropertyName = "packageDetailsUrl")]
    [JsonConverter(typeof(SafeUriConverter))]
    public Uri PackageDetailsUrl { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.Published)]
    public DateTimeOffset? Published { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.Owners)]
    [JsonConverter(typeof(MetadataFieldConverter))]
    public string Owners { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.PackageId)]
    public string PackageId { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.RequireLicenseAcceptance, DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(false)]
    [JsonConverter(typeof(SafeBoolConverter))]
    public bool RequireLicenseAcceptance { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.Summary)]
    public string Summary { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.Tags)]
    [JsonConverter(typeof(MetadataFieldConverter))]
    public string Tags { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.Title)]
    public string Title { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.Listed)]
    public bool IsListed { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.PrefixReserved)]
    public bool PrefixReserved { get; private set; }
    
    [JsonIgnore]
    public LicenseMetadata LicenseMetadata { get; }
    
    [JsonProperty(PropertyName = JsonProperties.Vulnerabilities)]
    public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; private set; }
    
    [JsonProperty(PropertyName = JsonProperties.SubjectId)]
    public Uri CatalogUri { get; private set; }

    // Constructor for json deserialization
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
        PackageId = registration.PackageId;
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