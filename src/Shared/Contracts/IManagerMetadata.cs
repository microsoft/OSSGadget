// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Contracts;

public interface IManagerMetadata
{
    /// <summary>
    /// The name of the package.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The version of the package.
    /// </summary>
    string Version { get; }
}