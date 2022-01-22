// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Helpers
{
    using NLog;
    using System;
    using System.IO;
    using System.Threading;

    public static class FileSystemHelper
    {
        static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Attempt to delete a directory and its contents with retries.
        /// </summary>
        /// <param name="path">The directory to delete.</param>
        /// <param name="recursive">If true, also delete all the contents of the directory.</param>
        /// <param name="attempts">Number of attempts. Minimum 1.</param>
        /// <param name="millisecondsDelay">Delay between attempts. Minimum 1.</param>
        /// <returns>If the directory was deleted.</returns>
        public static bool RetryDeleteDirectory(string path, bool recursive = true, int attempts = 5, int millisecondsDelay = 10)
        {
            if (path == null)
                return false;
            if (attempts < 1)
                attempts = 1;
            if (millisecondsDelay < 1)
                millisecondsDelay = 1;

            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive);
                    }
                    if (!Directory.Exists(path))
                    {
                        return true;
                    }
                }
                catch(Exception ex) when (ex is IOException)
                {
                    Thread.Sleep(millisecondsDelay);
                    Logger.Debug("Error deleting [{0}], sleeping for {1} seconds.", path, millisecondsDelay);
                }
            }

            return false;
        }
    }
}
