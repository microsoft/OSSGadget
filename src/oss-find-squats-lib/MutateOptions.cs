﻿// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats
{
    /// <summary>
    /// Options to provide to MutateExtension.EnumerateSquats
    /// </summary>
    public class MutateOptions
    {
        /// <summary>
        /// Number of milliseconds to sleep between requests to the project manager.
        /// </summary>
        public int SleepDelay { get; set; }
    }
}
