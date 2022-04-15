// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Utilities;

using NLog;
using NuGet.Common;
using System.Threading.Tasks;
using NuGetLogLevel = NuGet.Common.LogLevel;
using NLogLevel = NLog.LogLevel;

public class NuGetLogger : LoggerBase
{
    private readonly Logger _logger;

    /// <summary>
    /// Create a new instance of the <see cref="NuGetLogger"/>.
    /// This is a wrapper class to use a <see cref="NLog.Logger"/> in place of a <see cref="NuGet.Common.ILogger"/>
    /// </summary>
    /// <param name="logger">The <see cref="NLog.Logger"/> to map to.</param>
    public NuGetLogger(Logger logger)
    {
        _logger = logger;
    }

    public override void Log(ILogMessage message) => this._logger.Log(LogLevelConverter(message.Level), message);

    public override Task LogAsync(ILogMessage message)
    {
        this._logger.Log(LogLevelConverter(message.Level), message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Converts from the <see cref="NuGet.Common.LogLevel"/> to the <see cref="NLog.LogLevel"/>.
    /// </summary>
    /// <param name="level">The <see cref="NuGet.Common.LogLevel"/> to convert.</param>
    /// <returns>An equivalent <see cref="NLog.LogLevel"/>.</returns>
    private static NLogLevel LogLevelConverter(NuGetLogLevel level)
    {
        return level switch
        {
            NuGetLogLevel.Debug => NLogLevel.Debug,
            NuGetLogLevel.Verbose => NLogLevel.Trace,
            NuGetLogLevel.Information => NLogLevel.Info,
            NuGetLogLevel.Minimal => NLogLevel.Warn,
            NuGetLogLevel.Warning => NLogLevel.Warn,
            NuGetLogLevel.Error => NLogLevel.Error,
            _ => NLogLevel.Debug // Should never hit this, as we cover all 6 of the log levels.
        };
    }
}