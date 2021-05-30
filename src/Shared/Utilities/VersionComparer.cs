using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class VersionComparer : IComparer<List<string>>
    {

        /// <summary>
        /// Convert a version (string) to a list of version parts.
        /// Just splits into "numbers" and "non-numbers".
        /// </summary>
        /// <param name="version"></param>
        public static List<string> Parse(string version)
        {
            /*
             * version = version.ToLowerInvariant();
            if (version.StartsWith("v."))
            {
                version = version[2..];
            }
            if (Regex.IsMatch(version, "^v\\d"))
            {
                version = version[1..];
            }
            */

            var parts = new List<string>();
            var inNumber = false;
            var curStr = "";

            for (int i = 0; i < version.Length; i++)
            {
                var ch = version[i];
                if (Char.IsNumber(ch) ^ inNumber)
                {
                    if (curStr != "")
                    {
                        parts.Add(curStr);
                    }
                    curStr = "";
                }

                curStr += ch;
                if (i == version.Length - 1)
                {
                    parts.Add(curStr);
                }
                inNumber = Char.IsNumber(ch);
            }
            return parts;
        }

        /// <summary>
        /// Compares two lists of strings, using natural numbers where possible.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare(List<string>? x, List<string>? y)
        {
            if (x != null && y != null)
            {
                var minLength = Math.Min(x.Count, y.Count);
                for (int i=0; i<minLength; i++)
                {
                    if (int.TryParse(x[i], out int xInt) &&
                        int.TryParse(y[i], out int yInt))
                    {
                        if (xInt > yInt) return -1;
                        if (yInt > xInt) return 1;
                    }
                    else
                    {
                        var compValue = x[i].CompareTo(y[i]);
                        if (compValue != 0) return compValue;
                    }
                }

                // If we get here, the values are equal
                if (x.Count > y.Count) return -1;
                if (x.Count < y.Count) return 1;
                return 0;
            }
            if (x == null) return 1;
            if (y == null) return -1;
            return 0;
        }
    }
}
