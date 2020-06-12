using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    class StringOutputBuilder : IOutputBuilder
    {
        StringBuilder stringResults = new StringBuilder();

        /// <summary>
        /// Prints to the currently selected output
        /// </summary>
        public void PrintOutput()
        {
            Console.Out.Write(this.GetOutput());
        }

        public string? GetOutput()
        {
            return stringResults.ToString();
        }
        /// <summary>
        /// Overload of AppendOutput to add to text
        /// </summary>
        /// <param name="output"></param>
        public void AppendOutput(object? output)
        {
            var results = (IEnumerable<string>?)output ?? Array.Empty<String>().ToList();
            foreach (var line in results)
            {
                this.stringResults.Append(line);
                this.stringResults.Append(Environment.NewLine);
            }
        }
    }
}
