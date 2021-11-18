// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.CST.OpenSource.Shared
{
    public class ConsoleHelper
    {
        public static StreamWriter GetCurrentWriteStream()
        {
            return streamWriter ?? new StreamWriter(Console.OpenStandardOutput());
        }

        public static bool RedirectConsole(string outFile)
        {
            // switch Console Out to file
            if (!string.IsNullOrEmpty(outFile) && streamWriter == null)
            {
                fileStream = new FileStream(outFile, FileMode.OpenOrCreate, FileAccess.Write);
                streamWriter = new StreamWriter(fileStream);
                Console.SetOut(streamWriter);
                return true;
            }

            return false;
        }

        public static void RestoreConsole()
        {
            // switch back to Console
            StreamWriter standardOutput = new(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);

            streamWriter?.Close();
            fileStream?.Close();

            streamWriter?.Dispose();
            fileStream?.Dispose();

            fileStream = null;
            streamWriter = null;
        }

        private static FileStream? fileStream;
        private static StreamWriter? streamWriter;
    }
}