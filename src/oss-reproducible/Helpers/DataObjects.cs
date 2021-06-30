// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

                foreach (var result in Results)
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