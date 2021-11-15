// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.OpenSource.Model.Mutators
{
    /// <summary>
    /// Generates mutations of added or removed separators.
    /// Separators are '.', '-', '_'.
    /// </summary>
    public class SeparatorMutator : BaseMutator
    {
        public new string Mutator = "SEPARATOR";

        public static HashSet<char> separators = new() { '.', '-', '_' };

        public override IEnumerable<(string Name, string Reason)> Generate(string arg)
        {
            foreach (var s in separators)
            {
                if (arg.Contains(s))
                {
                    var rest = separators.Except(new[] { s });
                    foreach (var r in rest)
                    {
                        yield return (arg.Replace(s, r), Mutator + "_CHANGED");
                    }

                    // lastly remove separator
                    yield return (arg.Replace(s.ToString(), string.Empty), Mutator + "_REMOVED");
                }
            }
        }
    }
}