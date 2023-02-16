// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Helpers
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// A utilities class for string extension functions.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Replaces a character at a specified position in a string.
        /// </summary>
        /// <param name="str">The string have a character replaced in.</param>
        /// <param name="c">The character to insert into the string.</param>
        /// <param name="pos">The position in the string to replace with the provided character.</param>
        /// <returns>The string with the swapped character.</returns>
        public static string ReplaceCharAtPosition(this string str, char c, int pos)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }
            else if (pos >= 0 && pos < str.Length)
            {
                return string.Concat(str[..pos], c, str[(pos + 1)..]);
            }
            else
            {
                return str;
            }
        }

        public static bool IsBlank([NotNullWhen(false)]this string? str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static bool IsNotBlank([NotNullWhen(true)]this string? str)
        {
            return !str.IsBlank();
        }
        
        /// <summary>
        /// Returns the input string with a slash ('/') appended to it, unless there was already '/' at its end.
        /// </summary>
        /// <remarks>If the string is empty, no changes are made.</remarks>
        public static string EnsureTrailingSlash(this string url)
        {
            if (url.IsBlank())
            {
                return url;
            }

            url = url.TrimEnd(' ');
            if (!url.EndsWith('/'))
            {
                return url + '/';
            }

            return url;
        }
        
        /// <summary>
        /// Returns the input string <paramref name="source"/> with the first instance of <paramref name="oldValue"/>
        /// being replaced with <paramref name="newValue"/>.
        /// </summary>
        /// <remarks>If the string doesn't have an instance of <paramref name="oldValue"/>, no changes are made.</remarks>
        public static string ReplaceAtStart(this string source, string oldValue, string newValue)
        {
            int place = source.IndexOf(oldValue);
    
            return place == -1 ? source : $"{source[..place]}{newValue}{source[(place + oldValue.Length)..]}";
        }
        
        /// <summary>
        /// Returns the input string <paramref name="source"/> with the last instance of <paramref name="oldValue"/>
        /// being replaced with <paramref name="newValue"/>.
        /// </summary>
        /// <remarks>If the string doesn't have an instance of <paramref name="oldValue"/>, no changes are made.</remarks>
        public static string ReplaceAtEnd(this string source, string oldValue, string newValue)
        {
            int place = source.LastIndexOf(oldValue);
    
            return place == -1 ? source : $"{source[..place]}{newValue}{source[(place + oldValue.Length)..]}";
        }
    }
}