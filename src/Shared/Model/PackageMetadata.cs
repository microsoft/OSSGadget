using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model
{
    public class Downloads
    {
        [JsonProperty(PropertyName = "daily", NullValueHandling = NullValueHandling.Ignore)]
        public int? Daily { get; set; }

        [JsonProperty(PropertyName = "monthly", NullValueHandling = NullValueHandling.Ignore)]
        public int? Monthly { get; set; }

        [JsonProperty(PropertyName = "overall", NullValueHandling = NullValueHandling.Ignore)]
        public int? Overall { get; set; }

        [JsonProperty(PropertyName = "weekly", NullValueHandling = NullValueHandling.Ignore)]
        public int? Weekly { get; set; }

        [JsonProperty(PropertyName = "yearly", NullValueHandling = NullValueHandling.Ignore)]
        public int? Yearly { get; set; }
    }

    public class License
    {
        [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; set; }

        [JsonProperty(PropertyName = "spix_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? SPIX_ID { get; set; }

        [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
        public string? Url { get; set; }
    }

    /// <summary>
    ///     this class enables placeholders for data stored in other places. This can be local file, blob,
    ///     gist, or any other place where data can be stored
    /// </summary>
    public class LinkedData
    {
        // name of the tool (in case of analysis tools), name of the repo (in case of repository) etc the tool
        // storing this information will know what to do with it
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; set; }

        // type of pointer - blob/gist/file/...
        [JsonProperty(PropertyName = "pointer_type", NullValueHandling = NullValueHandling.Ignore)]
        public string? PointerType { get; set; }

        [JsonProperty(PropertyName = "pointer_link", NullValueHandling = NullValueHandling.Ignore)]
        public string? Rank { get; set; }
    }

    public class PackageMetadata
    {
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; set; }

        [JsonProperty(PropertyName = "description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get; set; }

        [JsonProperty(PropertyName = "all_versions", NullValueHandling = NullValueHandling.Ignore)]
        public List<Version>? AllVersions { get; set; }

        [JsonProperty(PropertyName = "authors", NullValueHandling = NullValueHandling.Ignore)]
        public List<User>? Authors { get; set; }

        [JsonProperty(PropertyName = "keywords", NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? Keywords { get; set; }

        [JsonProperty(PropertyName = "language", NullValueHandling = NullValueHandling.Ignore)]
        public string? Language { get; set; }

        [JsonProperty(PropertyName = "latest_package_version", NullValueHandling = NullValueHandling.Ignore)]
        public string? LatestPackageVersion { get; set; }

        [JsonProperty(PropertyName = "licenses", NullValueHandling = NullValueHandling.Ignore)]
        public List<License>? Licenses { get; set; }

        [JsonProperty(PropertyName = "maintainers", NullValueHandling = NullValueHandling.Ignore)]
        public List<User>? Maintainers { get; set; }

        [JsonProperty(PropertyName = "package_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? Package_Uri { get; set; }

        [JsonProperty(PropertyName = "package_manager_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? PackageManagerUri { get; set; }

        [JsonProperty(PropertyName = "package_version", NullValueHandling = NullValueHandling.Ignore)]
        public string? PackageVersion { get; set; }

        [JsonProperty(PropertyName = "platform", NullValueHandling = NullValueHandling.Ignore)]
        public string? Platform { get; set; }

        [JsonProperty(PropertyName = "repository", NullValueHandling = NullValueHandling.Ignore)]
        public List<Repository>? Repository { get; set; }

        [JsonProperty(PropertyName = "version_download_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? VersionDownloadUri { get; set; }

        [JsonProperty(PropertyName = "version_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? VersionUri { get; set; }

        // remote property bag
        [JsonProperty(PropertyName = "extended_data", NullValueHandling = NullValueHandling.Ignore)]
        public List<LinkedData>? ExtendedData { get; set; }

        // construct the json format for the metadata
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    public class User
    {
        [JsonProperty(PropertyName = "active_flag", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Active { get; set; }

        [JsonProperty(PropertyName = "email", NullValueHandling = NullValueHandling.Ignore)]
        public string? Email { get; set; }

        [JsonProperty(PropertyName = "Id", NullValueHandling = NullValueHandling.Ignore)]
        public int? Id { get; set; }

        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; set; }

        [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
        public string? Url { get; set; }
    }

    public class Version
    {   // ascending value of indexes to sort out the versions
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int? Index { get; set; }

        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        public string? VersionString { get; set; }
    }
}