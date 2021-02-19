using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    public class StringOutputBuilder : IOutputBuilder
    {
        /// <summary> Append more text to the result An incompatible object input will result in InvalidCast
        /// exception </summary> <param name="output">An IEnumerable<string> object</param>
        public void AppendOutput(IEnumerable<object> output)
        {
            stringResults.Append(string.Join(Environment.NewLine, (IEnumerable<string>)output));
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