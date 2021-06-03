using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    public class StringOutputBuilder : IOutputBuilder
    {
        /// <summary> Append more text to the result An incompatible object input will result in InvalidCast
        /// exception </summary> <param name="output">An IEnumerable<string> object</param>
        public void AppendOutput(IEnumerable<object> output)
        {
            foreach(var entry in output)
            {
                if (entry is string stringEntry)
                {
                    stringResults.Add(stringEntry);
                }
            }
        }

        public string GetOutput()
        {
            return string.Join(Environment.NewLine, stringResults);
        }

        /// <summary>
        ///     Prints to the currently selected output
        /// </summary>
        public void PrintOutput()
        {
            foreach(var result in stringResults)
            {
                Console.Out.WriteLine(result);
            }
        }

        /// <summary>
        ///     Write the output to the given file. Creating directory if needed.
        /// </summary>
        public void WriteOutput(string fileName)
        {
            using var fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            using var sw = new StreamWriter(fs);
            foreach(var result in stringResults)
            {
                sw.WriteLine(result);
            }
        }

        private List<string> stringResults = new List<string>();
    }
}