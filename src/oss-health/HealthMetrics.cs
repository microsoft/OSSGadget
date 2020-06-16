﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.CST.OpenSource.Health
{
    public class HealthMetrics
    {
        private const int MAX_HEALTH = 100;
        private const int MIN_HEALTH = 0;

        PackageURL purl;

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        public double CommitHealth { get; set; }
        public double PullRequestHealth { get; set; }
        public double IssueHealth { get; set; }
        public double SecurityIssueHealth { get; set; }
        public double ReleaseHealth;
        public double ContributorHealth { get; set; }
        public double RecentActivityHealth { get; set; }
        public double ProjectSizeHealth { get; set; }

        public HealthMetrics(PackageURL purl)
        {
            this.purl = purl;
        }
        public override string ToString()
        {
            Normalize();

            var sb = new StringBuilder();

            var properties = getHealthProperties();
            foreach (var property in properties.OrderBy(s => s.Name))
            {
                if (property.Name.EndsWith("Health"))
                {
                    var textualName = Regex.Replace(property.Name, "(\\B[A-Z])", " $1");
                    var result = Convert.ToDouble(property.GetValue(this));
                    var bar = new StringBuilder();
                    bar.Append("|");

                    //Create a ascii horizontal bar chart with one "*" for every full 5%
                    //And a "|" every 25% with a key on the bottom
                    for (int i = 1; i <= 20; i++)
                    {
                        if (result >= (i * 5)) // As long as the total is still greater than this multiple of 5
                        {
                            bar.Append("*");
                        }
                        else
                        {
                            bar.Append(" ");
                        }
                        if (i % 5 == 0)
                        {
                            bar.Append("|"); //Print a pipe after every five chars
                        }
                    }
                    //Space it out so it looks pretty
                    sb.AppendFormat("{0,24}: {1,25} {2:N2}%\n", textualName, bar, result);
                }
            }
            //Print the lower key, I'm sure there are better ways to do this.
            var key = "0%   25%   50%   75%   100%";
            sb.AppendFormat("{0,25} {1} \n", "", key);
            return sb.ToString();
        }
        PropertyInfo[] getHealthProperties()
        {
            return this.GetType().GetProperties(BindingFlags.NonPublic |
                                              BindingFlags.Public |
                                              BindingFlags.Instance);

        }
        public List<Result> toSarif()
        {
            BaseProjectManager? projectManager = ProjectManagerFactory.CreateProjectManager(purl, null);

            if (projectManager == null)
            {
                Logger.Error("Cannot determine the package type");
                return new List<Result>();
            }

            Normalize();

            List<Result> results = new List<Result>();
            var properties = getHealthProperties();
            foreach (var property in properties.OrderBy(s => s.Name))
            {
                if (property.Name.EndsWith("Health"))
                {
                    var textualName = Regex.Replace(property.Name, "(\\B[A-Z])", " $1");
                    var value = Convert.ToDouble(property.GetValue(this));

                    Result healthResult = new Result()
                    {
                        Kind = ResultKind.Review,
                        Level = FailureLevel.None,
                        Message = new Message()
                        {
                            Text = textualName
                        },
                        Rank = value,

                        Locations = SarifOutputBuilder.BuildPurlLocation(purl)
                    };
                    results.Add(healthResult);
                }
            }

            return results;
        }

                /**
                 * Normalizes all fields of this object.
                 */
                public void Normalize()
        {
            CommitHealth = NormalizeField(CommitHealth);
            PullRequestHealth = NormalizeField(PullRequestHealth);
            IssueHealth = NormalizeField(IssueHealth);
            SecurityIssueHealth = NormalizeField(SecurityIssueHealth);
            ReleaseHealth = NormalizeField(ReleaseHealth);
        }

        /**
         * Clamps a given value to [MIN_HEALTH..MAX_HEALTH] and rounds
         * to a single decimal point.
         */
        private static double NormalizeField(double value)
        {
            value = Math.Round(value, 1);
            if (value > MAX_HEALTH)
            {
                value = MAX_HEALTH;
            }
            if (value < MIN_HEALTH)
            {
                value = MIN_HEALTH;
            }
            return value;
        }
    }
}
