// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Shared
{
    using System;
    using System.Reflection;

    public static class CommonInitialization
    {
        /// <summary>
        ///     Prevent being initialized multiple times.
        /// </summary>
        private static bool Initialized = false;

        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Initializes common infrastructure, like logging.
        /// </summary>
        public static void Initialize()
        {
            // Only allow initialization once
            if (Initialized)
            {
                return;
            }

            Initialized = true;
        }
    }
}