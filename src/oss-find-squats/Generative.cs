using Scriban.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class Generative
    {
        private string[] _uh = new string[255];
        private string[] _ah = new string[255];
        private int[] _locations = new int[255];
        private string[] _keymap = new string[4];
        private HashSet<char> seprators = new HashSet<char> { '.', '-', '_' };

        public IList<Func<string, IEnumerable<(string, string)>>> Mutations { get; } = new List<Func<string, IEnumerable<(string, string)>>>();

        public Generative()
        {
            _uh['a'] = "αа⍺ａ𝐚𝑎𝒂𝒶𝓪𝔞𝕒𝖆𝖺𝗮𝘢𝙖𝚊𝛂𝛼𝜶𝝰𝞪";
            _uh['b'] = "ƄЬᏏᖯｂ𝐛𝑏𝒃𝒷𝓫𝔟𝕓𝖇𝖻𝗯𝘣𝙗𝚋";
            _uh['c'] = "ϲсᴄⅽⲥꮯｃ𐐽𝐜𝑐𝒄𝒸𝓬𝔠𝕔𝖈𝖼𝗰𝘤𝙘𝚌";
            _uh['d'] = "ԁᏧᑯⅆⅾꓒｄ𝐝𝑑𝒅𝒹𝓭𝔡𝕕𝖉𝖽𝗱𝘥𝙙𝚍";
            _uh['e'] = "еҽ℮ℯⅇꬲｅ𝐞𝑒𝒆𝓮𝔢𝕖𝖊𝖾𝗲𝘦𝙚𝚎";
            _uh['f'] = "ſϝքẝꞙꬵｆ𝐟𝑓𝒇𝒻𝓯𝔣𝕗𝖋𝖿𝗳𝘧𝙛𝚏𝟋";
            _uh['g'] = "ƍɡցᶃℊｇ𝐠𝑔𝒈𝓰𝔤𝕘𝖌𝗀𝗴𝘨𝙜𝚐";
            _uh['h'] = "һհᏂℎｈ𝐡𝒉𝒽𝓱𝔥𝕙𝖍𝗁𝗵𝘩𝙝𝚑";
            _uh['i'] = "ıɩɪ˛ͺιіӏᎥιℹⅈⅰ⍳ꙇꭵｉ𑣃𝐢𝑖𝒊𝒾𝓲𝔦𝕚𝖎𝗂𝗶𝘪𝙞𝚒𝚤𝛊𝜄𝜾𝝸𝞲";
            _uh['j'] = "ϳјⅉｊ𝐣𝑗𝒋𝒿𝓳𝔧𝕛𝖏𝗃𝗷𝘫𝙟𝚓";
            _uh['k'] = "ｋ𝐤𝑘𝒌𝓀𝓴𝔨𝕜𝖐𝗄𝗸𝘬𝙠𝚔";
            _uh['m'] = "ｍ";
            _uh['n'] = "ոռｎ𝐧𝑛𝒏𝓃𝓷𝔫𝕟𝖓𝗇𝗻𝘯𝙣𝚗";
            _uh['p'] = "ρϱр⍴ⲣｐ𝐩𝑝𝒑𝓅𝓹𝔭𝕡𝖕𝗉𝗽𝘱𝙥𝚙𝛒𝛠𝜌𝜚𝝆𝝔𝞀𝞎𝞺𝟈";
            _uh['q'] = "ԛգզｑ𝐪𝑞𝒒𝓆𝓺𝔮𝕢𝖖𝗊𝗾𝘲𝙦𝚚";
            _uh['r'] = "гᴦⲅꭇꭈꮁｒ𝐫𝑟𝒓𝓇𝓻𝔯𝕣𝖗𝗋𝗿𝘳𝙧𝚛";
            _uh['s'] = "ƽѕꜱꮪｓ𐑈𑣁𝐬𝑠𝒔𝓈𝓼𝔰𝕤𝖘𝗌𝘀𝘴𝙨𝚜";
            _uh['t'] = "ｔ𝐭𝑡𝒕𝓉𝓽𝔱𝕥𝖙𝗍𝘁𝘵𝙩𝚝";
            _uh['u'] = "ʋυսᴜꞟꭎꭒｕ𐓶𑣘𝐮𝑢𝒖𝓊𝓾𝔲𝕦𝖚𝗎𝘂𝘶𝙪𝚞𝛖𝜐𝝊𝞄𝞾";
            _uh['v'] = "νѵטᴠⅴ∨⋁ꮩｖ𑜆𑣀𝐯𝑣𝒗𝓋𝓿𝔳𝕧𝖛𝗏𝘃𝘷𝙫𝚟𝛎𝜈𝝂𝝼𝞶";
            _uh['w'] = "ɯѡԝաᴡꮃｗ𑜊𑜎𑜏𝐰𝑤𝒘𝓌𝔀𝔴𝕨𝖜𝗐𝘄𝘸𝙬𝚠𝞪";
            _uh['x'] = "×хᕁᕽ᙮ⅹ⤫⤬⨯ｘ𝐱𝑥𝒙𝓍𝔁𝔵𝕩𝖝𝗑𝘅𝘹𝙭𝚡";
            _uh['y'] = "ɣʏγуүყᶌỿℽꭚｙ𑣜𝐲𝑦𝒚𝓎𝔂𝔶𝕪𝖞𝗒𝘆𝘺𝙮𝚢𝛄𝛾𝜸𝝲𝞬";
            _uh['z'] = "ᴢꮓｚ𑣄𝐳𝑧𝒛𝓏𝔃𝔷𝕫𝖟𝗓𝘇𝘻𝙯𝚣";
            _uh['~'] = "˜῀⁓∼";
            _uh['.'] = "ɑαа⍺ａ𝐚𝑎𝒂𝒶𝓪𝔞𝕒𝖆𝖺𝗮𝘢𝙖𝚊𝛂𝛼𝜶𝝰𝞪";
            _uh['_'] = "ߺ﹍﹎﹏＿";
            _uh['-'] = "ˉ‾▔﹉﹊﹋﹌￣";

            _ah['a'] = "eoq4";
            _ah['b'] = "dp";
            _ah['c'] = "o";
            _ah['d'] = "bpq";
            _ah['e'] = "ao";
            _ah['f'] = "t";
            _ah['g'] = "q";
            _ah['h'] = "b";
            _ah['i'] = "lj";
            _ah['j'] = "il";
            _ah['l'] = "ij1";
            _ah['m'] = "n";
            _ah['n'] = "rmu";
            _ah['o'] = "ea0";
            _ah['p'] = "qg";
            _ah['q'] = "pg";
            _ah['r'] = "n";
            _ah['t'] = "f";

            Mutations.Add(_asciiHomoglyphs);
            Mutations.Add(_separators);
            Mutations.Add(_swapOrderOfLetters);
            Mutations.Add(_closeLetters);
            Mutations.Add(_prefixes);
            Mutations.Add(_sufixes);
            Mutations.Add(_duplicateEach);
            Mutations.Add(_afterSeparator);
            Mutations.Add(_substituteKnown);
            Mutations.Add(_unicodeHomoglphs);
            Mutations.Add(_bitFlips);
            Mutations.Add(_removeSome);
            Mutations.Add(_vowelSwap);
            Mutations.Add(_doubleHit);

            for (int i = 0; i < _locations.Length; i++)
            {
                _locations[i] = -1;
            }

            _keymap[0] = "1234567890-=";
            _keymap[1] = "qwertyuiop[]\\";
            _keymap[2] = "asdfghjkl;'";
            _keymap[3] = "zxcvbnm,./";

            _locations['1'] = 0;
            _locations['2'] = 1;
            _locations['3'] = 2;
            _locations['4'] = 3;
            _locations['5'] = 4;
            _locations['6'] = 5;
            _locations['7'] = 6;
            _locations['8'] = 7;
            _locations['9'] = 8;
            _locations['0'] = 9;
            _locations['-'] = 10;
            _locations['='] = 11;

            _locations['q'] = 100;
            _locations['w'] = 101;
            _locations['e'] = 102;
            _locations['r'] = 103;
            _locations['t'] = 104;
            _locations['y'] = 105;
            _locations['u'] = 106;
            _locations['i'] = 107;
            _locations['o'] = 108;
            _locations['p'] = 109;
            _locations['['] = 110;
            _locations[']'] = 111;
            _locations['\\'] = 111;

            _locations['a'] = 200;
            _locations['s'] = 201;
            _locations['d'] = 202;
            _locations['f'] = 203;
            _locations['g'] = 204;
            _locations['h'] = 205;
            _locations['j'] = 206;
            _locations['k'] = 207;
            _locations['l'] = 208;
            _locations[';'] = 219;
            _locations['\''] = 210;

            _locations['z'] = 300;
            _locations['x'] = 301;
            _locations['c'] = 302;
            _locations['v'] = 303;
            _locations['b'] = 304;
            _locations['n'] = 305;
            _locations['m'] = 306;
            _locations[','] = 307;
            _locations['.'] = 308;
            _locations['/'] = 309;
        }

        public Dictionary<string, IEnumerable<string>> Mutate(string arg)
        {
            var mutations = Mutations.SelectMany(m => m(arg));
            var result = new Dictionary<string, IEnumerable<string>>();
            foreach (var mutation in mutations)
            {
                if (result.ContainsKey(mutation.Item1))
                {
                    result[mutation.Item1].Append(mutation.Item2);
                }
                else
                {
                    result[mutation.Item1] = new List<string>{ mutation.Item2 };
                }
            }

            return result;
        }

        private IEnumerable<(string, string)> _unicodeHomoglphs(string arg)
        {
            // assumption is that attacker is making just one change
            for (int i = 0; i < arg.Length; i++)
            {
                if (_uh[arg[i]] != null)
                {
                    foreach (var c in _uh[arg[i]])
                    {
                        yield return (string.Join(arg.Substring(0, i), c, arg.Substring(i)), "unicode homoglyph");
                    }
                }
            }
        }

        private IEnumerable<(string, string)> _asciiHomoglyphs(string arg)
        {
            // assumption is that attacker is making just one change
            for (int i = 0; i < arg.Length; i++)
            {
                if (arg[i] >= 0 && arg[i] <= _ah.Length && _ah[arg[i]] != null)
                {
                    foreach (var c in _ah[arg[i]])
                    {
                        yield return (string.Concat(arg.Substring(0, i), c.ToString(), arg.Substring(i + 1)), "ascii homoglyph");
                    }
                }
            }
        }

        private IEnumerable<(string, string)> _separators(string arg)
        {
            foreach (var s in seprators)
            {
                if (arg.Contains(s))
                {
                    var rest = seprators.Except(new char[] { s });
                    foreach (var r in rest)
                    {
                        yield return (arg.Replace(s, r), "separator chanaged");
                    }

                    // lastly remove separator
                    yield return (arg.Replace(s.ToString(), string.Empty), "separator removed");
                }
            }
        }


        private IEnumerable<(string, string)> _swapOrderOfLetters(string arg)
        {
            if (arg.Length < 5)
            {
                yield break;
            }

            for (int i = 1; i < arg.Length - 1; i++)
            {
                yield return (string.Concat(arg.Substring(0, i - 1), arg[i + 1], arg[i], arg.Substring(i + 2, arg.Length - (i + 2))), "letter swapped");
            }
        }

        private IEnumerable<(string, string)> _closeLetters(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = _getNeighbors(arg[i], _keymap, _locations).ToList();

                foreach (var c in n)
                {
                    yield return (string.Concat(arg.Substring(0, i), c, arg.Substring(i + 1)), "close letters on keymap");
                }
            }
        }

        private IEnumerable<(string, string)> _doubleHit(string arg)
        {
            for (int i = 0; i < arg.Length; i++)
            {
                var n = _getNeighbors(arg[i], _keymap, _locations).ToList();

                foreach (var c in n)
                {
                    yield return (string.Concat(arg.Substring(0, i), c, arg.Substring(i)), "double hit close letters on keymap");
                }
            }
        }

        private IEnumerable<char> _getNeighbors(char c, string[] keymap, int[] locs)
        {
            if (c >= locs.Length)
            {
                yield break;
            }

            var loc = locs[c];
            if (loc == -1)
            {
                yield break;
            }

            int yOrigin = loc / 100;
            int xOrigin = loc % 100;
            var neighbors = new int[][]
            {
                new int[] { xOrigin - 1, yOrigin - 1 }, new int[] { xOrigin, yOrigin - 1}, new int[] { xOrigin + 1, yOrigin - 1 },
                new int[] { xOrigin - 1, yOrigin     }                                   , new int[] { xOrigin + 1, yOrigin     },
                new int[] { xOrigin - 1, yOrigin + 1 }, new int[] { xOrigin, yOrigin + 1}, new int[] { xOrigin + 1, yOrigin + 1 },
            };

            foreach (var n in neighbors)
            {
                int x = n[0];
                int y = n[1];
                if (x < 0 || y < 0)
                {
                    continue;
                }

                if (y > 3)
                {
                    continue;
                }

                var row = keymap[y];

                if (x >= row.Length)
                {
                    continue;
                }

                yield return row[x];
            }
        }

        private IEnumerable<(string, string)> _prefixes(string arg)
        {
            var prefixes = new string[] { ".", "x", "-", "X", "_" };
            return prefixes.Select(s => (string.Concat(s, arg), "prefix added"));
        }

        private IEnumerable<(string, string)> _sufixes(string arg)
        {
            var suffixes = new string[] { "s", "1", "2", "3", "4", "5", "ng", "-ng", ".", "x", "-", "_", "js" };
            return suffixes.Select(s => (string.Concat(arg, s), "suffix added"));
        }

        private IEnumerable<(string, string)> _substituteKnown(string arg)
        {
            if (arg.Contains("js"))
            {
                yield return (arg.Replace("js", "javascript"), "js to javascript");
            }

            if (arg.Contains("javascript"))
            {
                yield return (arg.Replace("javascript", "js"), "javascript to js");
            }
        }

        private IEnumerable<(string, string)> _afterSeparator(string arg)
        {
            foreach (var s in seprators)
            {
                var splits = arg.Split(s, StringSplitOptions.RemoveEmptyEntries);

                if (splits.Count() == 2)
                {
                    for (var c = 'a'; c <= 'z'; c++)
                    {
                        yield return (splits[0] + s + c + splits[1].Substring(1), "letter change after seprator");
                    }
                }
            }
        }

        private IEnumerable<(string, string)> _duplicateEach(string arg)
        {
            for (int i = 0; i < arg.Length - 2; i++)
            {
                yield return (arg.Substring(0, i + 1) + arg[i].ToString() + arg.Substring(i + 2), "letter duplicated");
            }
        }

        private IEnumerable<(string, string)> _bitFlips(string arg)
        {
            var byteArray = Encoding.UTF8.GetBytes(arg);
            for (int i = 0; i < byteArray.Length; i++)
            {
                for(int j = 0; j < 8; j++)
                {
                    byte mask = (byte)(1 << j);
                    byteArray[i] = (byte)(byteArray[i] ^ mask);
                    var newString = Encoding.UTF8.GetString(byteArray);
                    var valid = true;
                    
                    for(int k = 0; k < newString.Length && valid; k++){
                        if (!Uri.IsWellFormedUriString(newString, UriKind.Relative))
                        {
                            valid = false;
                        }
                    }
                    if (valid)
                    {
                        yield return (newString, "Bit Flips");
                    }
                }
            }
        }

        private IEnumerable<(string, string)> _numbers(string arg)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<(string, string)> _removeSome(string arg)
        {
            for(var i = 1; i < arg.Length; i++)
            {
                yield return ($"{arg[0..i]}{arg[(i + 1)..]}", "Remove Character");
            }
        }

        private IEnumerable<(string, string)> _vowelSwap(string arg)
        {
            char[] vowels = { 'a', 'e', 'i', 'o', 'u', 'y', 'A', 'E', 'I', 'O', 'U', 'Y'};
            char[] chars = Encoding.UTF8.GetChars(Encoding.UTF8.GetBytes(arg));
            for(var i = 0; i < chars.Length; i++)
            {
                char old = chars[i];
                if (vowels.Contains(old))
                {
                    for (var j = 0; j < vowels.Length; j++)
                    {
                        if (vowels[j] != old)
                        {
                            chars[i] = vowels[j];
                            if (Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(chars)) is string s)
                            {
                                yield return (s, "Vowel Swap");
                            }
                        }
                    }
                    chars[i] = old;
                }
            }
        }

        private IEnumerable<(string, string)> _capitalize(string arg)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<(string, string)> _envDependent(string arg)
        {
            throw new NotImplementedException();
        }
    }
}
