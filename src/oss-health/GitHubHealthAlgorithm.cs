// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Octokit;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Health
{
    using PackageUrl;

    /// <summary>
    ///     Actual implementation for Github project health
    /// </summary>
    internal class GitHubHealthAlgorithm : BaseHealthAlgorithm
    {
        //private static readonly string API_VERSION = "v2.2";

        /// <summary>
        ///     GitHub client access object.
        /// </summary>
        private readonly IGitHubClient Client;

        /// <summary>
        ///     Maximum wait time per call to the GitHub API.
        /// </summary>
        private const int MAX_TIME_WAIT_MS = 1000 * 30;

        /// <summary>
        ///     Default options for calls to the GitHub API.
        /// </summary>
        private static readonly ApiOptions DEFAULT_API_OPTIONS = new ApiOptions { PageCount = 5, PageSize = 100 };

        /// <summary>
        ///     Access token used when connecting to GitHub. Multiple allowed, separated by a comma.
        ///     Will be automatically set from matching Environment variable on class construction. 
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string? ENV_GITHUB_ACCESS_TOKEN { get; set; } = null;

        /// <summary>
        ///     User Agent used when connecting to GitHub.
        ///     Will be automatically set from matching Environment variable on class construction. 
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        public string ENV_HTTPCLIENT_USER_AGENT { get; set; } = "GitHubProjectHealth";

        /// <summary>
        ///     PackageURL to analyze
        /// </summary>
        private readonly PackageURL purl;

        public GitHubHealthAlgorithm(PackageURL purl)
        {
            if (purl == null || purl.Type != "github")
            {
                throw new ArgumentException("Only GitHub-based PackageURLs can be analyzed.");
            }

            this.purl = purl;

            if (string.IsNullOrWhiteSpace(ENV_GITHUB_ACCESS_TOKEN))
            {
                throw new ArgumentException("Missing GitHub access token. Please define GITHUB_ACCESS_TOKEN.");
            }

            // Choose a random token from the list
            string[]? githubAccessTokens = ENV_GITHUB_ACCESS_TOKEN.Split(new char[] { ',' });

#pragma warning disable SEC0115 // Insecure Random Number Generator
#pragma warning disable SCS0005 // Weak random generator
            string? githubAccessToken = githubAccessTokens.ElementAt(new Random().Next(githubAccessTokens.Count()));
#pragma warning restore SCS0005 // Weak random generator
#pragma warning restore SEC0115 // Insecure Random Number Generator

            Client = new GitHubClient(new ProductHeaderValue(ENV_HTTPCLIENT_USER_AGENT))
            {
                Credentials = new Credentials(githubAccessToken)
            };
            Client.SetRequestTimeout(TimeSpan.FromMilliseconds(MAX_TIME_WAIT_MS));
        }

        /// <summary>
        ///     Wrapper for Github calls for each component of the overall health.
        /// </summary>
        /// <returns> </returns>
        public override async Task<HealthMetrics> GetHealth()
        {
            Logger.Debug("GetHealth({0}/{1})", purl.Namespace, purl.Name);

            HealthMetrics? health = new HealthMetrics(purl);
            MiscellaneousRateLimit? initialRateLimit = await Client.Miscellaneous.GetRateLimits();
            int remaining = initialRateLimit.Resources.Core.Remaining;
            if (remaining < 500)
            {
                Logger.Warn("Your GitHub access token has only {0}/5000 tokens left.", remaining);
            }
            if (remaining == 0)
            {
                throw new Exception("No remaining API tokens.");
            }

            await GetProjectSizeHealth(health);
            await GetIssueHealth(health);
            await GetCommitHealth(health);
            await GetContributorHealth(health);
            await GetRecentActivityHealth(health);
            await GetReleaseHealth(health);
            await GetPullRequestHealth(health);

            return health;
        }

        /// <summary>
        ///     Calculate health based on project size. If the size is less than 100, then the health is the
        ///     size of the project. If it's between 100 and 5000, it's 100%. If it's higher then it starts to
        ///     approach zero.
        /// </summary>
        /// <returns> </returns>
        public async Task GetProjectSizeHealth(HealthMetrics metrics)
        {
            Repository? repo = await Client.Repository.Get(purl.Namespace, purl.Name);
            long health = 100;
            if (repo.Size < 100)
            {
                health = repo.Size;
            }
            else if (repo.Size > 10000)
            {
                health = 500000 / repo.Size;
            }
            metrics.ProjectSizeHealth = Clamp(health);
        }

        /// <summary>
        ///     Calculates commit health.
        /// </summary>
        /// <returns> </returns>
        public async Task GetCommitHealth(HealthMetrics metrics)
        {
            Logger.Debug($"GetCommitHealth('{purl.Namespace}', '{purl.Name}')");

            double weightedCommits = 0.0;
            int totalCommits = 0;
            int totalRecentCommits = 0;

            System.Collections.Generic.IReadOnlyList<Contributor>? contributors = await Client.Repository.Statistics.GetContributors(purl.Namespace, purl.Name);
            totalCommits = contributors.Sum(c => c.Total);

            CommitActivity? recentCommitActivity = await Client.Repository.Statistics.GetCommitActivity(purl.Namespace, purl.Name);
            foreach (WeeklyCommitActivity? weekActivity in recentCommitActivity.Activity)
            {
                totalRecentCommits += weekActivity.Total;
                int numDaysAgo = (DateTime.Now - weekActivity.WeekTimestamp).Days;
                weightedCommits += (6.0 * weekActivity.Total / Math.Log(numDaysAgo + 14));
            }

            double commitHealth = 7.0 * Math.Log(totalCommits) + weightedCommits;
            metrics.CommitHealth = Math.Round(Clamp(commitHealth), 1);
        }

        /// <summary>
        ///     Calculates pull request health
        /// </summary>
        /// <param name="metrics"> </param>
        /// <returns> </returns>
        public async Task GetPullRequestHealth(HealthMetrics metrics)
        {
            Logger.Debug($"GetPullRequestHealth('{purl.Namespace}', '{purl.Name}')");
            double pullRequestHealth = 0;

            int pullRequestOpen = 0;
            int pullRequestClosed = 0;

            PullRequestRequest? pullRequestRequest = new PullRequestRequest()
            {
                State = ItemStateFilter.All
            };

            System.Collections.Generic.IReadOnlyList<PullRequest>? pullRequests = await Client.PullRequest.GetAllForRepository(purl.Namespace, purl.Name, pullRequestRequest, DEFAULT_API_OPTIONS);

            // Gather raw data
            foreach (PullRequest? pullRequest in pullRequests)
            {
                if (pullRequest.State == ItemState.Open)
                {
                    pullRequestOpen++;
                }
                else if (pullRequest.State == ItemState.Closed)
                {
                    pullRequestClosed++;
                }
            }
            if (pullRequestOpen + pullRequestClosed > 0)
            {
                pullRequestHealth = 100.0 * pullRequestClosed / (pullRequestOpen + pullRequestClosed);
            }
            metrics.PullRequestHealth = pullRequestHealth;
        }

        /**
         * Calculates contributor health for a Repository.
         *
         * This is defined as 6 * (# contributors, subscribers, forks, stargazers), capped at 100
         */

        public async Task GetContributorHealth(HealthMetrics metrics)
        {
            Logger.Trace("GetContributorHealth({0}, {1}", purl.Namespace, purl.Name);

            Repository? repository = await Client.Repository.Get(purl.Namespace, purl.Name);
            double contributorHealth = 6.0 * repository.StargazersCount +
                                             repository.WatchersCount +
                                             repository.ForksCount;

            if (contributorHealth < 100.0)
            {
                System.Collections.Generic.IReadOnlyList<RepositoryContributor>? contributors = await Client.Repository.GetAllContributors(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
                contributorHealth += 6.0 * contributors.Count;
            }

            metrics.ContributorHealth = Clamp(contributorHealth);
        }

        /**
         * Determine the most recent activity (any type).
         *
         * The API only returns events from the last 90 days
         *
         * Most recent activity.
         * Health calculated as the mean number of seconds in the past 30 activities.
         */

        public async Task GetRecentActivityHealth(HealthMetrics metrics)
        {
            Logger.Trace("GetRecentActivityHealth({0}, {1})", purl.Namespace, purl.Name);

            System.Collections.Generic.IReadOnlyList<Activity>? activityList = await Client.Activity.Events.GetAllForRepository(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
            double activityHealth = 0.50 * activityList.Count;
            metrics.RecentActivityHealth = Clamp(activityHealth);
        }

        /**
         * Release health is defined as:
         * # releases + # tags
         */

        public async Task GetReleaseHealth(HealthMetrics metrics)
        {
            Logger.Trace("GetReleaseHealth({0}, {1})", purl.Namespace, purl.Name);

            System.Collections.Generic.IReadOnlyList<Release>? releases = await Client.Repository.Release.GetAll(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
            double releaseHealth = releases.Count;

            System.Collections.Generic.IReadOnlyList<RepositoryTag>? tags = await Client.Repository.GetAllTags(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
            releaseHealth += tags.Count;
            metrics.ReleaseHealth = Clamp(releaseHealth);
        }

        /// <summary>
        ///     Retrieves issue and security issue counts (subset) to minimize calls out.
        ///     Note: Octokit Search API was found earlier to be unreliable
        /// </summary>
        /// <returns> </returns>
        public async Task GetIssueHealth(HealthMetrics metrics)
        {
            Logger.Trace("GetIssueHealth({0}, {1})", purl.Namespace, purl.Name);

            string[]? securityFlags = new string[] {
                    "security", "insecure", "vulnerability", "cve", "valgrind", "xss",
                    "sqli ", "vulnerable", "exploit", "fuzz", "injection",
                    "buffer overflow", "valgrind", "sql injection", "csrf",
                    "xsrf", "pwned", "akamai legacy ssl", "bad cipher ordering",
                    "untrusted", "backdoor", "command injection"
            };

            RepositoryIssueRequest? filter = new RepositoryIssueRequest
            {
                Filter = IssueFilter.All,
                State = ItemStateFilter.All,
                Since = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(3 * 365))
            };

            int openIssues = 0;
            int closedIssues = 0;
            int openSecurityIssues = 0;
            int closedSecurityIssues = 0;

            System.Collections.Generic.IReadOnlyList<Issue>? issues = await Client.Issue.GetAllForRepository(purl.Namespace, purl.Name, filter);
            foreach (Issue? issue in issues)
            {
                // filter out pull requests
                if (issue.Url.Contains("/pull") || issue.HtmlUrl.Contains("/pull"))
                {
                    continue;
                }

                //general issue status
                if (issue.State == ItemState.Open)
                {
                    openIssues++;
                }
                else if (issue.State == ItemState.Closed)
                {
                    closedIssues++;
                }

                // security status check within applicable fields
                string? labels = string.Join(",", issue.Labels.Select(l => l.Name));
                string? content = issue.Title + issue.Body + labels;

                if (securityFlags.Any(s => content.Contains(s, StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (issue.State == ItemState.Open)
                    {
                        openSecurityIssues++;
                    }
                    else if (issue.State == ItemState.Closed)
                    {
                        closedSecurityIssues++;
                    }
                }
            }
            double issueHealth = 0.0;

            if (openIssues + closedIssues > 0)
            {
                issueHealth = 30.0 * openIssues / (openIssues + closedIssues);
            }
            else
            {
                issueHealth = 50.0;
            }

            metrics.IssueHealth = issueHealth;

            double securityIssueHealth = 0.0;

            if (openSecurityIssues + closedSecurityIssues > 0)
            {
                securityIssueHealth = (double)openSecurityIssues / (double)(openSecurityIssues + closedSecurityIssues);
            }
            else
            {
                securityIssueHealth = 60.0;  // Lose a little credit if project never had a security issue
            }

            metrics.SecurityIssueHealth = securityIssueHealth;

            return;
        }
    }
}
