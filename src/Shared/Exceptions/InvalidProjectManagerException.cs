// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Exceptions
{
    using PackageUrl;
    using System;

    /// <summary>
    /// Exception thrown when the PackageURL has an invalid manager..
    /// </summary>
    public class InvalidProjectManagerException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidProjectManagerException"/> class.
        /// </summary>
        public InvalidProjectManagerException(PackageURL packageUrl)
            : base($"The package URL: {packageUrl} has an invalid Project Manager.")
        {
        }
    }
}