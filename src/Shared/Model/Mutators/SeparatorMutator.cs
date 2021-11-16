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

        /// <summary>
        /// Initializes a <see cref="SeparatorMutator"/> instance.
        /// Optionally takes in a additional separators, or a list of overriding separators to
        /// replace the default list with.
        /// </summary>
        /// <param name="additionalSeparators">An optional parameter for extra separators.</param>
        /// <param name="overrideSeparators">An optional parameter for list of separators to replace the
        /// default list with.</param>
        public SeparatorMutator(char[]? additionalSeparators = null, char[]? overrideSeparators = null)
        {
            if (overrideSeparators != null)
            {
                separators = new HashSet<char>(overrideSeparators);
            }
            if (additionalSeparators != null)
            {
                separators = new HashSet<char>(separators.Concat(additionalSeparators));
            }
        }

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