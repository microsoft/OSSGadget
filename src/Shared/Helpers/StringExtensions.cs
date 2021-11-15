// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

namespace Microsoft.CST.OpenSource.Helpers
{
    public static class StringExtensions
    {
        /// <summary>
        /// Replaces a character at a specified position in a string.
        /// </summary>
        /// <param name="str">The string have a character replaced in.</param>
        /// <param name="c">The character to insert into the string.</param>
        /// <param name="pos">The position in the string to replace with the provided character..</param>
        /// <returns>The string with the swapped character.</returns>
        public static string ReplaceCharAtPosition(this string str, char c, int pos)
        {
            return string.IsNullOrEmpty(str) ? str : string.Concat(str[..pos], c, str[(pos + 1)..]);
        }
    }
}