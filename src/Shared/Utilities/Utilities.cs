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
    }
}