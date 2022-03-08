// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    using PackageUrl;

    internal class IgnoreFilter
    {
        private static readonly List<string> FilterText;

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initialiizes the ignore filter using the embedded resource.
        /// </summary>
        static IgnoreFilter()
        {
            Assembly? assembly = Assembly.GetExecutingAssembly();
            string? resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("PackageIgnoreList.txt"));
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);

            FilterText = new List<string>();

            if (stream != null)
            {
                using StreamReader reader = new StreamReader(stream);
                string? line;
                while ((line = reader?.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!line.Contains(":") || line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                    {
                        continue;  // Not a valid pattern
                    }
                    Logger.Trace("Adding {0} to filter.", line);
                    FilterText.Add(line);
                }
            }
            else
            {
                Logger.Warn("Unable to find PackageIgnoreList.txt.");
            }
        }

        /// <summary>
        /// Checks to see if a given file should be ignored when making a comparison.
        /// </summary>
        /// <param name="packageUrl"></param>
        /// <param name="strategyName"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        internal static bool IsIgnored(PackageURL? packageUrl, string strategyName, string filePath)
        {
            bool shouldIgnore = false;
            filePath = filePath.Replace("\\", "/").Trim();
            foreach (string? filter in FilterText)
            {
                string[]? parts = filter.Split(':', 3);
                if (parts.Length != 3)
                {
                    continue;   // Invalid line
                }
                string? _packageManager = parts[0].Trim();
                string? _strategy = parts[1].Trim();
                string? _regex = parts[2].Trim();

                if (string.Equals(_packageManager, "*", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(_packageManager, packageUrl?.Type ?? "*", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.Equals(_strategy, "*", StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(strategyName, _strategy, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (Regex.IsMatch(filePath, _regex, RegexOptions.IgnoreCase))
                        {
                            shouldIgnore = true;
                            break;
                        }
                    }
                }
            }
            Logger.Trace("IsIgnored({0}, {1}, {2} => {3}", packageUrl, strategyName, filePath, shouldIgnore);
            return shouldIgnore;
        }
    }
}