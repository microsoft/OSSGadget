using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    class IgnoreFilter
    {
        private static readonly List<string> FilterText;

        static IgnoreFilter()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("PackageIgnoreList.txt"));
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

                    FilterText.Add(line);
                }
            }
        }

        internal static bool IsIgnored(PackageURL? packageUrl, string strategyName, string filePath)
        {
            filePath = filePath.Replace("\\", "/").Trim();

            foreach (var filter in FilterText)
            {
                var parts = filePath.Split(':', 3);
                if (parts.Length != 3)
                {
                    continue;   // Invalid line
                }
                var _packageManager = parts[0].Trim();
                var _strategy = parts[1].Trim();
                var _regex = parts[2].Trim();

                if (string.Equals(_packageManager, "*", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(_packageManager, packageUrl?.Type ?? "*", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.Equals(strategyName, "*", StringComparison.InvariantCultureIgnoreCase) || 
                        string.Equals(strategyName, _strategy, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return Regex.IsMatch(filePath, _regex, RegexOptions.IgnoreCase);
                    }
                }
            }
            return false;
        }
    }
}
