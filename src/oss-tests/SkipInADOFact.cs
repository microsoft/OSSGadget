// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Tests;

public class SkipInADOFactAttribute : FactAttribute
{
    public SkipInADOFactAttribute()
    {
        bool runningInCI = Environment.GetEnvironmentVariable("TF_BUILD")?.ToLowerInvariant() == "true";

        if (runningInCI)
        {
            Skip = "Test skipped when run in CI/ADO";
        }
    }
}

