// Copyright (c) Microsoft Corporation. Licensed under the MIT License.
using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Shared
{
    public class VersionComparer : IComparer<List<string>>
    {

        /// <summary>
        /// Convert a version (string) to a list of version parts.
        /// Just splits into "numbers" and "non-numbers".
        /// </summary>
        /// <param name="version"></param>
        public static List<string> Parse(string version)
        {
            List<string> parts = new();
            bool inNumber = false;
            string curStr = string.Empty;

            for (int i = 0; i < version.Length; i++)
            {
                char ch = version[i];
                if (char.IsNumber(ch) ^ inNumber)
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
                inNumber = char.IsNumber(ch);
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
                int minLength = Math.Min(x.Count, y.Count);
                for (int i = 0; i < minLength; i++)
                {
                    if (int.TryParse(x[i], out int xInt) &&
                        int.TryParse(y[i], out int yInt))
                    {
                        if (xInt > yInt) return -1;
                        if (yInt > xInt) return 1;
                    }
                    else
                    {
                        int compValue = x[i].CompareTo(y[i]);
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
