// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    public class Downloads
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
        // storing and reading this information will know what to do with it
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; set; }

        // type of pointer - blob/gist/file/...
        [JsonProperty(PropertyName = "pointer_type", NullValueHandling = NullValueHandling.Ignore)]
        public string? PointerType { get; set; }

        [JsonProperty(PropertyName = "pointer_link", NullValueHandling = NullValueHandling.Ignore)]
        public string? Pointer_Link { get; set; }
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

        [JsonProperty(PropertyName = "downloads", NullValueHandling = NullValueHandling.Ignore)]
        public Downloads? Downloads { get; set; }

        [JsonProperty(PropertyName = "latest_package_version", NullValueHandling = NullValueHandling.Ignore)]
        public string? LatestPackageVersion { get; set; }

        [JsonProperty(PropertyName = "licenses", NullValueHandling = NullValueHandling.Ignore)]
        public List<License>? Licenses { get; set; }

        [JsonProperty(PropertyName = "maintainers", NullValueHandling = NullValueHandling.Ignore)]
        public List<User>? Maintainers { get; set; }

        [JsonProperty(PropertyName = "package_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? PackageUri { get; set; }

        [JsonProperty(PropertyName = "api_package_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? ApiPackageUri { get; set; }

        [JsonProperty(PropertyName = "package_manager_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? PackageManagerUri { get; set; }
        
        [JsonProperty(PropertyName = "homepage", NullValueHandling = NullValueHandling.Ignore)]
        public string? Homepage { get; set; }

        [JsonProperty(PropertyName = "package_version", NullValueHandling = NullValueHandling.Ignore)]
        public string? PackageVersion { get; set; }

        [JsonProperty(PropertyName = "signature", NullValueHandling = NullValueHandling.Ignore)]
        public List<Digest>? Signature { get; set; }

        [JsonProperty(PropertyName = "platform", NullValueHandling = NullValueHandling.Ignore)]
        public string? Platform { get; set; }

        [JsonProperty(PropertyName = "size", NullValueHandling = NullValueHandling.Ignore)]
        public long? Size { get; set; }

        [JsonProperty(PropertyName = "upload_time", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? UploadTime { get; set; }

        [JsonProperty(PropertyName = "commit_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? CommitId { get; set; }

        [JsonProperty(PropertyName = "repository", NullValueHandling = NullValueHandling.Ignore)]
        public List<Repository>? Repository { get; set; }

        [JsonProperty(PropertyName = "repository_commit_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? RepositoryCommitID { get; set; }

        [JsonProperty(PropertyName = "version_download_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? VersionDownloadUri { get; set; }

        [JsonProperty(PropertyName = "version_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? VersionUri { get; set; }

        [JsonProperty(PropertyName = "api_version_uri", NullValueHandling = NullValueHandling.Ignore)]
        public string? ApiVersionUri { get; set; }

        [JsonProperty(PropertyName = "active_flag", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Active { get; set; }

        // remote property bag
        [JsonProperty(PropertyName = "extended_data", NullValueHandling = NullValueHandling.Ignore)]
        public List<LinkedData>? ExtendedData { get; set; }

        [JsonProperty(PropertyName = "dependencies", NullValueHandling = NullValueHandling.Ignore)]
        public List<Dependency>? Dependencies { get; set; }

        [JsonProperty(PropertyName = "install_scripts", NullValueHandling = NullValueHandling.Ignore)]
        public List<Command>? Scripts { get; set; }

        // construct the json format for the metadata
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
        
        /// <summary>
        /// Converts the json/ToString() output of PackageMetadata into a PackageMetadata object.
        /// </summary>
        /// <param name="json">The JSON representation of a <see cref="PackageMetadata"/> object.</param>
        /// <returns>The <see cref="PackageMetadata"/> object constructed from the json.</returns>
        public static PackageMetadata? FromJson(string json)
        {
            return JsonConvert.DeserializeObject<PackageMetadata>(json);
        }
    }

    public record User
    {
        [JsonProperty(PropertyName = "active_flag", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Active { get; init; }

        [JsonProperty(PropertyName = "email", NullValueHandling = NullValueHandling.Ignore)]
        public string? Email { get; init; }

        [JsonProperty(PropertyName = "Id", NullValueHandling = NullValueHandling.Ignore)]
        public int? Id { get; init; }

        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; init; }

        [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
        public string? Url { get; init; }
    }

    public class Version
    {   // ascending value of indexes to sort out the versions
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int? Index { get; set; }

        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        public string? VersionString { get; set; }
    }

    public class Digest
    {
        [JsonProperty(PropertyName = "signature_algorithm", NullValueHandling = NullValueHandling.Ignore)]
        public string? Algorithm { get; set; }

        [JsonProperty(PropertyName = "signature", NullValueHandling = NullValueHandling.Ignore)]
        public string? Signature { get; set; }
    }

    public class Dependency
    {
        [JsonProperty(PropertyName = "package", NullValueHandling = NullValueHandling.Ignore)]
        public string? Package { get; set; }
        
        [JsonProperty(PropertyName = "framework", NullValueHandling = NullValueHandling.Ignore)]
        public string? Framework { get; set; }
    }

    public class Command
    {
        [JsonProperty(PropertyName = "command", NullValueHandling = NullValueHandling.Ignore)]
        public string? CommandLine { get; set; }

        [JsonProperty(PropertyName = "arguments", NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? Arguments { get; set; }
    }
}