// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Model.Enums;

/// <summary>
/// Special cases used for json parsing.
/// </summary>
public enum JsonParsingOption
{
    /// <summary>
    /// No specific option.
    /// </summary>
    None = 0,

    /// <summary>
    /// Used in Cargo only right now as their files aren't formatted correctly for json.
    /// <example>https://raw.githubusercontent.com/rust-lang/crates.io-index/master/ra/nd/rand</example>
    /// </summary>
    NotInArrayNotCsv,
}