// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.CST.OpenSource.Shared;
using System.Threading.Tasks;
using Octokit;

using CSTRepository = Microsoft.CST.OpenSource.Model.Repository;
using GHRepository = Octokit.Repository;
using NLog;

namespace Microsoft.CST.OpenSource.Model
{
    public class Repository
    {
        public const string ENV_GITHUB_ENDPOINT = "https://github.com";
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        // JSON properties
        [JsonProperty(PropertyName = "Id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }

        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; set; }

        [JsonProperty(PropertyName = "description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get; set; }

        [JsonProperty(PropertyName = "archived_flag", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Archived { get; set; }

        [JsonProperty(PropertyName = "contributors", NullValueHandling = NullValueHandling.Ignore)]
        public List<User>? Contributors { get; set; }

        [JsonProperty(PropertyName = "created_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty(PropertyName = "downloads", NullValueHandling = NullValueHandling.Ignore)]
        public Downloads? Downloads { get; set; }

        [JsonProperty(PropertyName = "followers", NullValueHandling = NullValueHandling.Ignore)]
        public int? FollowersCount { get; set; }

        [JsonProperty(PropertyName = "forks_count", NullValueHandling = NullValueHandling.Ignore)]
        public int? Forks { get; set; }

        [JsonProperty(PropertyName = "homepage", NullValueHandling = NullValueHandling.Ignore)]
        public string? Homepage { get; set; }

        [JsonProperty(PropertyName = "is_fork", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsFork { get; set; }

        [JsonProperty(PropertyName = "language", NullValueHandling = NullValueHandling.Ignore)]
        public string? Language { get; set; }

        [JsonProperty(PropertyName = "licenses", NullValueHandling = NullValueHandling.Ignore)]
        public List<License>? Licenses { get; set; }

        [JsonProperty(PropertyName = "linked_data", NullValueHandling = NullValueHandling.Ignore)]
        public LinkedData? LinkedData { get; set; }

        [JsonProperty(PropertyName = "maintainers", NullValueHandling = NullValueHandling.Ignore)]
        public List<User>? Maintainers { get; set; }

        [JsonProperty(PropertyName = "openissues_count", NullValueHandling = NullValueHandling.Ignore)]
        public int? OpenIssuesCount { get; set; }

        [JsonProperty(PropertyName = "owner", NullValueHandling = NullValueHandling.Ignore)]
        public User? Owner { get; set; }

        [JsonProperty(PropertyName = "parent", NullValueHandling = NullValueHandling.Ignore)]
        public string? Parent { get; set; }

        [JsonProperty(PropertyName = "purl", NullValueHandling = NullValueHandling.Ignore)]
        public string? Purl { get; set; }

        [JsonProperty(PropertyName = "pushed_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? PushedAt { get; set; }

        [JsonProperty(PropertyName = "rank", NullValueHandling = NullValueHandling.Ignore)]
        public double? Rank { get; set; }

        [JsonProperty(PropertyName = "size", NullValueHandling = NullValueHandling.Ignore)]
        public long? Size { get; set; }

        [JsonProperty(PropertyName = "stakeholders_count", NullValueHandling = NullValueHandling.Ignore)]
        public int? StakeholdersCount { get; set; }

        [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
        public string? Type { get; set; }

        [JsonProperty(PropertyName = "updated_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
        public string? Uri { get; set; }

        public async Task<CSTRepository?> ExtractRepositoryMetadata(PackageURL purl)
        {
            if (purl is not null && purl.ToString() is string purlString && !string.IsNullOrWhiteSpace(purlString))
            {
                if (purl.Type != "github")
                {
                    Logger.Debug("Only github repos are handled currently");
                    return this;
                }

                return await FetchGithubRepositoryMetadata(purl);
            }
            return null;
        }

        private async Task<CSTRepository?> FetchGithubRepositoryMetadata(PackageURL purl)
        {
            try
            {
                var github = new GitHubClient(new ProductHeaderValue("OSSGadget"));
                var ghRepository = await github.Repository.Get(purl.Namespace, purl.Name);

                if (ghRepository is null) { return null; }
                Archived = ghRepository.Archived;
                CreatedAt = ghRepository.CreatedAt;
                UpdatedAt = ghRepository.UpdatedAt;
                Description = Utilities.GetMaxClippedLength(ghRepository.Description);
                IsFork = ghRepository.Fork;
                Forks = ghRepository.ForksCount;
                Homepage = Utilities.GetMaxClippedLength(ghRepository.Homepage);
                Id = ghRepository.Id;
                Language = Utilities.GetMaxClippedLength(ghRepository.Language);
                Name = ghRepository.Name;
                OpenIssuesCount = ghRepository.OpenIssuesCount;
                Parent = ghRepository.Parent?.Url;
                PushedAt = ghRepository.PushedAt;
                Size = ghRepository.Size;
                FollowersCount = ghRepository.StargazersCount;
                Uri = Utilities.GetMaxClippedLength(ghRepository.Url);
                StakeholdersCount = ghRepository.WatchersCount;

                if (ghRepository.License is not null)
                {
                    Licenses ??= new List<License>();
                    Licenses.Add(new License()
                    {
                        Name = ghRepository.License.Name,
                        Url = ghRepository.License.Url,
                        SPIX_ID = ghRepository.License.SpdxId
                    });
                }

                Owner ??= new User()
                {
                    Id = ghRepository.Owner.Id,
                    Name = ghRepository.Owner.Name,
                    Email = ghRepository.Owner.Email,
                    Url = ghRepository.Owner.Url,
                    Active = !ghRepository.Owner.Suspended
                };
            }
            catch (Exception ex)
            {
                Logger.Debug($"Exception occurred while retrieving repository data: {ex}");
            }
            return this;
        }
    }
}