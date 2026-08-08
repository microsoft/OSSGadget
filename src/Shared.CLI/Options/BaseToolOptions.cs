// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.Options;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

public class BaseToolOptions
{
    [Option('l', "log-level", Required = false, Default = "Info",
        HelpText = "Set the logging level (Trace=0, Debug=1, Info=2, Warn=3, Error=4, Fatal=5, Off=6). Can use name or number.")]
    public string LogLevel { get; set; } = "Info";
}
