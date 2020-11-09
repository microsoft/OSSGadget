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
            try
            {
                if (element?.GetProperty(keyName) is JsonElement res)
                {
                    return res;
                }
            }
            catch (KeyNotFoundException)
            {
            }
            return null;
        }

        public static string GetMaxClippedLength(string str)
        {
            int MaxLength = Math.Min(str.Length, MAX_FIELD_LENGTH);
            return str[0..MaxLength];
        }

        private const int MAX_FIELD_LENGTH = 65535;
    }
}