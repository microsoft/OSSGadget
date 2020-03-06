// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.OpenSource.Health
{
    public class HealthMetrics
    {
        private const int MAX_HEALTH = 100;
        private const int MIN_HEALTH = 0;

        public double CommitHealth { get; set; }
        public double PullRequestHealth { get; set; }
        public double IssueHealth { get; set; }
        public double SecurityIssueHealth { get; set; }
        public double ReleaseHealth;
        public double ContributorHealth { get; set; }
        public double RecentActivityHealth { get; set; }
        public double ProjectSizeHealth { get; set; }

        public override string ToString()
        {
            Normalize();

            var sb = new StringBuilder();
            var properties = this.GetType().GetProperties(BindingFlags.NonPublic |
                                                          BindingFlags.Public |
                                                          BindingFlags.Instance);
            foreach (var property in properties.OrderBy(s => s.Name))
            {
                if (property.Name.EndsWith("Health"))
                {
                    var textualName = Regex.Replace(property.Name, "(\\B[A-Z])", " $1");
                    sb.AppendFormat("{0}: {1:N1}%\n", textualName, property.GetValue(this));
                }
            }
            return sb.ToString();
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
