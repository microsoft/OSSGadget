// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats
{
    using System.Collections.Generic;

    /// <summary>
    /// A helper for utilities related to the QWERTY keyboard.
    /// </summary>
    public static class QwertyKeyboardHelper
    {
        /// <summary>
        /// The keymap of an ANSI QWERTY Keyboard. (USA style QWERTY Keyboard)
        /// </summary>
        private static readonly string[] _keymap = { "1234567890-=", "qwertyuiop[]\\", "asdfghjkl;'", "zxcvbnm,./", };

        private static readonly Dictionary<char, int> _locations = new()
        {
            ['1'] = 0,
            ['2'] = 1,
            ['3'] = 2,
            ['4'] = 3,
            ['5'] = 4,
            ['6'] = 5,
            ['7'] = 6,
            ['8'] = 7,
            ['9'] = 8,
            ['0'] = 9,
            ['-'] = 10,
            ['='] = 11,
            ['q'] = 100,
            ['w'] = 101,
            ['e'] = 102,
            ['r'] = 103,
            ['t'] = 104,
            ['y'] = 105,
            ['u'] = 106,
            ['i'] = 107,
            ['o'] = 108,
            ['p'] = 109,
            ['['] = 110,
            [']'] = 111,
            ['\\'] = 111,
            ['a'] = 200,
            ['s'] = 201,
            ['d'] = 202,
            ['f'] = 203,
            ['g'] = 204,
            ['h'] = 205,
            ['j'] = 206,
            ['k'] = 207,
            ['l'] = 208,
            [';'] = 209,
            ['\''] = 210,
            ['z'] = 300,
            ['x'] = 301,
            ['c'] = 302,
            ['v'] = 303,
            ['b'] = 304,
            ['n'] = 305,
            ['m'] = 306,
            [','] = 307,
            ['.'] = 308,
            ['/'] = 309,
        };

        /// <summary>
        /// Gets the neighboring characters on the keyboard.
        /// </summary>
        /// <param name="c">The keyboard character to get the neighboring characters for.</param>
        /// <returns>A list of the neighboring characters.</returns>
        public static IEnumerable<char> GetNeighboringCharacters(char c)
        {
            // See if dictionary contains the given character.
            if (!_locations.ContainsKey(c))
            {
                yield break;
            }

            // Get the integer location value for this character.
            int loc = _locations[c];

            int yOrigin = loc / 100;
            int xOrigin = loc % 100;

            // Calculate the neighboring character locations.
            int[][]? neighbors = new[]
            {
                new[] {xOrigin - 1, yOrigin - 1}, new[] {xOrigin, yOrigin - 1}, new[] {xOrigin + 1, yOrigin - 1},
                new[] {xOrigin - 1, yOrigin}, new[] {xOrigin + 1, yOrigin}, new[] {xOrigin - 1, yOrigin + 1},
                new[] {xOrigin, yOrigin + 1}, new[] {xOrigin + 1, yOrigin + 1},
            };

            // Loop through the 8 neighboring characters.
            foreach (int[]? n in neighbors)
            {
                int x = n[0];
                int y = n[1];

                // Assert that the neighboring character's location is a valid location on a keyboard.
                if (x < 0 || y is < 0 or > 3)
                {
                    continue;
                }

                // Get the row this neighboring character position is on.
                string? row = _keymap[y];

                // Assert that the x coordinate for this neighboring character position is a valid position on this row.
                if (x >= row.Length)
                {
                    continue;
                }

                // Return the neighboring character.
                yield return row[x];
            }
        }
    }
}