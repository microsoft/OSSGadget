// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Helpers;

using PackageUrl;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class PackageUrlJsonConverter : JsonConverter<PackageURL>
{
    /// <summary>
    /// Read the <see cref="PackageURL"/> as a string back to the object.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to get the json string.</param>
    /// <param name="typeToConvert">The Type to convert to.</param>
    /// <param name="options">The custom <see cref="JsonSerializerOptions"/> options for the converter.</param>
    /// <returns>The <see cref="PackageURL"/> from the json text.</returns>
    public override PackageURL Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => new(reader.GetString());

    /// <summary>
    /// Write the <see cref="PackageURL"/> as a string instead of converting it to a json object itself.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write the json string.</param>
    /// <param name="packageUrl">The <see cref="PackageURL"/> to write.</param>
    /// <param name="options">The custom <see cref="JsonSerializerOptions"/> options for the converter.</param>
    public override void Write(
        Utf8JsonWriter writer,
        PackageURL packageUrl,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(packageUrl.ToString());
}