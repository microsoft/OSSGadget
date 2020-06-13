using Microsoft.CodeAnalysis.Sarif;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    public interface IOutputBuilder
    {
        public void AppendOutput(IEnumerable<object>? output);
        public string? GetOutput();
        public void PrintOutput();
    }
}
