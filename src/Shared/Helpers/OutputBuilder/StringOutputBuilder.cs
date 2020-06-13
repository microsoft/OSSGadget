using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    internal class StringOutputBuilder : IOutputBuilder
    {
        /// <summary>
        ///     Append more text to the result
        /// </summary>
        /// <param name="output"> </param>
        public void AppendOutput(IEnumerable<object>? output)
        {
            if (output is IEnumerable<string> results)
            {
                stringResults.Append(string.Join(Environment.NewLine, results));
            }
        }

        public string GetOutput()
        {
            return stringResults.ToString();
        }

        /// <summary>
        ///     Prints to the currently selected output
        /// </summary>
        public void PrintOutput()
        {
            Console.Out.Write(this.GetOutput());
        }

        private StringBuilder stringResults = new StringBuilder();
    }
}