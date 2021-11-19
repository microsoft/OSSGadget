// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Shared
{
    using System.Collections.Generic;

    public class NoneOutputBuilder : IOutputBuilder
    {
        /// <summary> 
        /// An output builder that doesn't do anything.
        /// </summary>
        /// <param name="output">An IEnumerable<string> object</param>
        public void AppendOutput(IEnumerable<object> output)
        {
        }

        public string GetOutput()
        {
            return string.Empty;
        }

        /// <summary>
        ///     Prints to the currently selected output
        /// </summary>
        public void PrintOutput()
        {
        }

        public void WriteOutput(string fileName) { }
    }
}