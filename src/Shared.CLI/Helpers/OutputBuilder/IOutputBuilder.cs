// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Shared
{
    using System.Collections.Generic;

    /// <summary>
    ///     Interface for implementing Output providers
    /// </summary>
    public interface IOutputBuilder
    {
        /// <summary>
        ///     Append the format output by passing in compatible IEnumerable input object An incompatible
        ///     object input will result in InvalidCast exception
        /// </summary>
        /// <param name="output"> </param>
        public void AppendOutput(IEnumerable<object> output);

        /// <summary>
        ///     Gets the format output in string representation
        /// </summary>
        /// <returns> </returns>
        public string GetOutput();

        /// <summary>
        ///     Print the format string representation to the currently selected output
        /// </summary>
        public void PrintOutput();

        /// <summary>
        ///     Write the output to the given file. Creating directory if needed.
        /// </summary>
        public void WriteOutput(string fileName);
    }
}