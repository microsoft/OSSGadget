// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats
{
    using Mutators;
    using System.Collections.Generic;

    /// <summary>
    /// Options to provide to MutateExtension.EnumerateSquats
    /// </summary>
    public class MutateOptions
    {
        /// <summary>
        /// Number of milliseconds to sleep between requests to the project manager.
        /// </summary>
        public int SleepDelay { get; set; }

        /// <summary>
        /// If the cache should be used when checking if mutations exist.
        /// </summary>
        public bool UseCache { get; set; } = true;
        
        /// <summary>
        /// Mutators that should be excluded.
        /// </summary>
        public IEnumerable<MutatorType>? ExcludedMutators { get; set; }
    }
}
