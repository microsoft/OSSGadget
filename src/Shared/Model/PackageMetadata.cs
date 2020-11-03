using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Model
{
    public class License
    {
        [JsonProperty(PropertyName = "spix_id")]
        public string? SPIX_ID { get; set; }

        [JsonProperty(PropertyName = "type")]
        public string? Type { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string? Url { get; set; }
    }

    public class PackageMetadata
    {
        public PackageMetadata()
        {
            AllVersions = new List<Version>();
            Maintainers = new List<User>();
            Authors = new List<User>();
            Repository = new List<Repository>();
            Keywords = new List<string>();
            Licenses = new List<License>();
        }

        [JsonProperty(PropertyName = "all_versions")]
        public List<Version> AllVersions { get; set; }

        [JsonProperty(PropertyName = "authors")]
        public List<User> Authors { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string? Description { get; set; }

        [JsonProperty(PropertyName = "keywords")]
        public List<string> Keywords { get; set; }

        [JsonProperty(PropertyName = "language")]
        public string? Language { get; set; }

        [JsonProperty(PropertyName = "latest_package_version")]
        public string? LatestPackageVersion { get; set; }

        [JsonProperty(PropertyName = "licenses")]
        public List<License> Licenses { get; set; }

        [JsonProperty(PropertyName = "maintainers")]
        public List<User> Maintainers { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string? Name { get; set; }

        [JsonProperty(PropertyName = "package_uri")]
        public string? Package_Uri { get; set; }

        [JsonProperty(PropertyName = "package_manager_uri")]
        public string? PackageManagerUri { get; set; }

        [JsonProperty(PropertyName = "package_version")]
        public string? PackageVersion { get; set; }

        [JsonProperty(PropertyName = "platform")]
        public string? Platform { get; set; }

        [JsonProperty(PropertyName = "repository")]
        public List<Repository> Repository { get; set; }

        [JsonProperty(PropertyName = "version_download_uri")]
        public string? VersionDownloadUri { get; set; }

        [JsonProperty(PropertyName = "version_uri")]
        public string? VersionUri { get; set; }

        // construct the json format for the metadata
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class Repository
    {
        [JsonProperty(PropertyName = "rank")]
        public double Rank { get; set; }

        [JsonProperty(PropertyName = "type")]
        public string? Type { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string? Uri { get; set; }
    }

    public class User
    {
        [JsonProperty(PropertyName = "email")]
        public string? Email { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string? Name { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string? Url { get; set; }
    }

    public class Version
    {
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        [JsonProperty(PropertyName = "version")]
        public string? VersionString { get; set; }
    }
}