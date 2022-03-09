// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource;

using Helpers;
using System.Text.Json;

public class OssGadgetJsonSerializer
{
    // Custom json serialization converter for PackageURLs
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters =
        {
            new PackageUrlJsonConverter()
        }
    };
}