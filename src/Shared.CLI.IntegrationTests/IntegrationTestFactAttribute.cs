// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.OSSGadget.Shared.Cli.IntegrationTests;

public static class IntegrationTestEnvironmentHelper
{
    public static bool IsRunningInCI()
    {
        return Environment.GetEnvironmentVariable("TF_BUILD")?.ToLowerInvariant() == "true";
    }
}

public class IntegrationTestFactAttribute : FactAttribute
{
    public IntegrationTestFactAttribute()
    {
        if(IntegrationTestEnvironmentHelper.IsRunningInCI())
        {
            Skip = "Integration test skipped in ADO CI";
        }
    }
}

public class IntegrationTestTheoryAttribute : TheoryAttribute
{
    public IntegrationTestTheoryAttribute()
    {
        if (IntegrationTestEnvironmentHelper.IsRunningInCI())
        {
            Skip = "Integration test skipped in ADO CI";
        }
    }
}