using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    public class ConsoleHelper
    {
        private static StreamWriter streamWriter;

        public static void RedirectConsole(string outFile)
        {
            if (!string.IsNullOrEmpty(outFile) && streamWriter == null)
            {
                streamWriter = new StreamWriter(outFile);
                Console.SetOut(streamWriter);
            }
        }

        public static void RestoreConsole()
        {
            if(streamWriter != null)
            {
                streamWriter.Flush();
                streamWriter.Close();
                streamWriter.Dispose();
                var standardOutput = new StreamWriter(Console.OpenStandardOutput());
                standardOutput.AutoFlush = true;
                Console.SetOut(standardOutput);
            }
        }
    }
}
