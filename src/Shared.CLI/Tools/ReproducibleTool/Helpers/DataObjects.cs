// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.Reproducibility
{
    public class ReproducibleToolResult
    {
        public string? PackageUrl { get; set; }

        public bool IsReproducible
        {
            get
            {
                if (Results == null)
                {
                    return false;
                }

                foreach (StrategyResult? result in Results)
                {
                    if (result.IsSuccess && !result.IsError)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public List<StrategyResult>? Results { get; set; }
    }
}