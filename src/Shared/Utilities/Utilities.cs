using Microsoft.CST.OpenSource.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class Utilities
    {
        public static JsonElement? GetJSONPropertyIfExists(JsonElement? element, string keyName)
        {
            if (element is JsonElement elem && !string.IsNullOrWhiteSpace(keyName))
            {
                try
                {
                    if (elem.GetProperty(keyName) is JsonElement res)
                    {
                        return res;
                    }
                }
                catch (KeyNotFoundException)
                {
                }
            }
            return null;
        }

        public static string? GetJSONPropertyStringIfExists(JsonElement? element, string keyName)
        {
            return GetJSONPropertyIfExists(element, keyName)?.ToString();
        }

        public static string GetMaxClippedLength(string str)
        {
            if (string.IsNullOrEmpty(str)) { return str; }
            int MaxLength = Math.Min(str.Length, MAX_FIELD_LENGTH);
            return str[0..MaxLength];
        }

        /// <summary>
        ///     Convert a JSON array (or a csv string) into an POCO array
        /// </summary>
        /// <param name="jsonElement"> </param>
        /// <returns> </returns>
        public static List<string>? ConvertJSONToList(JsonElement? jsonElement)
        {
            List<string> items = new List<string>();

            if (jsonElement.ToString() is string str)
            {
                if (GetJSONEnumerator(jsonElement) is JsonElement.ArrayEnumerator enumerator)
                {
                    foreach (JsonElement itemElement in enumerator)
                    {
                        if (itemElement.GetString() is string item)
                        {
                            items.Add(item);
                        }
                    }
                }
                else if (str.Contains(","))
                {
                    // split the string as a csv
                    items = str.Split(',').ToList();
                }
                else
                {
                    return null;
                }
            }

            return items;
        }

        public static JsonElement.ArrayEnumerator? GetJSONEnumerator(JsonElement? jsonElement)
        {
            if (jsonElement is not null && jsonElement?.ToString() is string str && !string.IsNullOrEmpty(str))
            {
                if (str.Contains("["))
                {
                    if (jsonElement?.EnumerateArray() is JsonElement.ArrayEnumerator enumerator)
                    {
                        return enumerator;
                    }
                }
            }

            return null;
        }

        public static bool ValueExists<T>(T? item, out T retItem) where T : new()
        {
            if (item is T x && x != null)
            {
                retItem = x;
                return true;
            }
            retItem = default(T) ?? new T();
            return false;
        }

        private const int MAX_FIELD_LENGTH = 65535;
    }
}