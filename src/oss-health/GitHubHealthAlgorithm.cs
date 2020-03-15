// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;
using Octokit;

namespace Microsoft.CST.OpenSource.Health
{

    /// <summary>
    /// Actual implementation for Github project health
    /// </summary>
    class GitHubHealthAlgorithm : BaseHealthAlgorithm
    {
        //private static readonly string API_VERSION = "v2.2";

        /// <summary>
        /// GitHub client access object.
        /// </summary>
        private readonly IGitHubClient Client;

        /// <summary>
        /// Maximum wait time per call to the GitHub API.
        /// </summary>
        private const int MAX_TIME_WAIT_MS = 1000 * 30;

        /// <summary>
        /// Default options for calls to the GitHub API.
        /// </summary>
        private static readonly ApiOptions DEFAULT_API_OPTIONS = new ApiOptions { PageCount = 5, PageSize = 100 };


        /// <summary>
        /// Access token used when connecting to GitHub.
        /// Multiple allowed, separated by a comma.
        /// Updated automatically during program start.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        private static string ENV_GITHUB_ACCESS_TOKEN = null;

        /// <summary>
        /// User Agent used when connecting to GitHub.
        /// Updated automatically during program start, if needed.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        private static string ENV_HTTPCLIENT_USER_AGENT = "GitHubProjectHealth";

        /// <summary>
        /// PackageURL to analyze
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
            var githubAccessTokens = ENV_GITHUB_ACCESS_TOKEN.Split(new char[] { ',' });

            #pragma warning disable SEC0115 // Insecure Random Number Generator
            #pragma warning disable SCS0005 // Weak random generator
            var githubAccessToken = githubAccessTokens.ElementAt(new Random().Next(githubAccessTokens.Count()));
            #pragma warning restore SCS0005 // Weak random generator
            #pragma warning restore SEC0115 // Insecure Random Number Generator

            Client = new GitHubClient(new ProductHeaderValue(ENV_HTTPCLIENT_USER_AGENT))
            {
                Credentials = new Credentials(githubAccessToken)
            };
            Client.SetRequestTimeout(TimeSpan.FromMilliseconds(MAX_TIME_WAIT_MS));
        }

        /// <summary>
        /// Wrapper for Github calls for each component of the overall health.
        /// </summary>
        /// <returns></returns>
        public override async Task<HealthMetrics> GetHealth()
        {
            Logger.Debug("GetHealth({0}/{1})", purl.Namespace, purl.Name);

            var health = new HealthMetrics();
            var initialRateLimit = await Client.Miscellaneous.GetRateLimits();
            var remaining = initialRateLimit.Resources.Core.Remaining;
            if (remaining < 500)
            {
                Logger.Warn("Your GitHub access token has only {0}/5000 tokens left.");
            }
            if (remaining == 0)
            {
                throw new Exception("No remaining API tokens.");
            }

            health.ProjectSizeHealth = await GetProjectSizeHealth();
            health.IssueHealth = await GetIssueHealth();
            health.CommitHealth = await GetCommitHealth();
            health.ContributorHealth = await GetContributorHealth();
            health.RecentActivityHealth = await GetRecentActivityHealth();
            health.ReleaseHealth = await GetReleaseHealth();
            health.PullRequestHealth = await GetPullRequestHealth();

            return health;
        }

        /// <summary>
        /// Calculate health based on project size.
        /// If the size is less than 100, then the health is the size of the
        /// project. If it's between 100 and 5000, it's 100%. If it's higher
        /// then it starts to approach zero.
        /// </summary>
        /// <returns></returns>
        public async Task<double> GetProjectSizeHealth()
        {
            var repo = await Client.Repository.Get(purl.Namespace, purl.Name);
            long health = 100;
            if (repo.Size < 100)
            {
                health = repo.Size;
            }
            else if (repo.Size > 10000)
            {
                health = 500000 / repo.Size;
            }
            return Clamp(health);
        }

        /// <summary>
        /// Calculates commit health.
        /// </summary>
        /// <returns></returns>
        public async Task<double> GetCommitHealth()
        {
            Logger.Debug($"GetCommitHealth('{purl.Namespace}', '{purl.Name}')");

            double weightedCommits = 0.0;
            int totalCommits = 0;
            int totalRecentCommits = 0;

            var contributors = await Client.Repository.Statistics.GetContributors(purl.Namespace, purl.Name);
            totalCommits = contributors.Sum(c => c.Total);

            var recentCommitActivity = await Client.Repository.Statistics.GetCommitActivity(purl.Namespace, purl.Name);
            foreach (var weekActivity in recentCommitActivity.Activity)
            {
                totalRecentCommits += weekActivity.Total;
                var numDaysAgo = (DateTime.Now - weekActivity.WeekTimestamp).Days;
                weightedCommits += (6.0 * weekActivity.Total / Math.Log(numDaysAgo + 14));
            }

            double commitHealth = 7.0 * Math.Log(totalCommits) + weightedCommits;
            return Math.Round(Clamp(commitHealth), 1);
        }

        public async Task<double> GetPullRequestHealth()
        {
            Logger.Debug($"GetPullRequestHealth('{purl.Namespace}', '{purl.Name}')");
            double pullRequestHealth = 0;

            int pullRequestOpen = 0;
            int pullRequestClosed = 0;

            var pullRequestRequest = new PullRequestRequest()
            {
                State = ItemStateFilter.All
            };

            var pullRequests = await Client.PullRequest.GetAllForRepository(purl.Namespace, purl.Name, pullRequestRequest, DEFAULT_API_OPTIONS);

            // Gather raw data
            foreach (var pullRequest in pullRequests)
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
            return pullRequestHealth;
        }

        /**
         * Calculates contributor health for a Repository.
         * 
         * This is defined as 6 * (# contributors, subscribers, forks, stargazers), capped at 100
         */
        public async Task<double> GetContributorHealth()
        {
            Logger.Debug($"GetContributorHealth('{purl.Namespace}', '{purl.Name}')");

            var repository = await Client.Repository.Get(purl.Namespace, purl.Name);
            double contributorHealth = 6.0 * repository.StargazersCount +
                                             repository.SubscribersCount +
                                             repository.ForksCount;

            if (contributorHealth < 100.0)
            {
                var contributors = await Client.Repository.GetAllContributors(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
                contributorHealth += 6.0 * contributors.Count;
            }

            return Clamp(contributorHealth);
        }

        /**
         * Determine the most recent activity (any type).
         * 
         * The API only returns events from the last 90 days
         * 
         * Most recent activity.
         * Health calculated as the mean number of seconds in the past 30 activities.
         */
        public async Task<double> GetRecentActivityHealth()
        {
            Logger.Debug($"GetRecentActivityHealth('{purl.Namespace}', '{purl.Name}')");

            var activityList = await Client.Activity.Events.GetAllForRepository(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
            double activityHealth = 0.50 * activityList.Count;
            return Clamp(activityHealth);
        }


        /**
         * Release health is defined as:
         * # releases + # tags
         */
        public async Task<double> GetReleaseHealth()
        {
            Logger.Debug($"GetReleaseHealth('{purl.Namespace}', '{purl.Name}')");

            var releases = await Client.Repository.Release.GetAll(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
            double releaseHealth = releases.Count();

            var tags = await Client.Repository.GetAllTags(purl.Namespace, purl.Name, DEFAULT_API_OPTIONS);
            releaseHealth += tags.Count();
            return Clamp(releaseHealth);
        }



        /// <summary>
        /// Retrieves issue and security issue counts (subset) to minimize calls out.  
        /// Note: Octokit Search API was found earlier to be unreliable
        /// </summary>
        /// <returns></returns>
        public async Task<double> GetIssueHealth()
        {
            Logger.Debug($"GetIssueHealth('{purl.Namespace}', '{purl.Name}')");

            var securityFlags = new string[] {
                    "security", "insecure", "vulnerability", "cve", "valgrind", "xss",
                    "sqli ", "vulnerable", "exploit", "fuzz", "injection",
                    "buffer overflow", "valgrind", "sql injection", "csrf",
                    "xsrf", "pwned", "akamai legacy ssl", "bad cipher ordering",
                    "untrusted", "backdoor", "command injection"
            };

            var filter = new RepositoryIssueRequest
            {
                Filter = IssueFilter.All,
                State = ItemStateFilter.All,
                Since = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(3 * 365))
            };

            int openIssues = 0;
            int closedIssues = 0;
            int openSecurityIssues = 0;
            int closedSecurityIssues = 0;

            var issues = await Client.Issue.GetAllForRepository(purl.Namespace, purl.Name, filter);
            foreach (var issue in issues)
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
                var labels = string.Join(",", issue.Labels.Select(l => l.Name));
                var content = issue.Title + issue.Body + labels;

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
            if (openSecurityIssues + closedSecurityIssues > 0)
            {
                issueHealth += 70.0 * openSecurityIssues / (openSecurityIssues + closedSecurityIssues);
            }
            else
            {
                issueHealth += 60.0;  // Lose a little credit if project never had a security issue
            }

            return Clamp(issueHealth);
        }
    }
}
