// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using CommandLine;
    using CommandLine.Text;
    using Helpers;
    using Microsoft.CST.OpenSource.Shared;
    using OssGadget.Options;
    using PackageManagers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

    public abstract class BaseTool<T> : OssGadgetLib where T: BaseToolOptions
    {
        public OutputFormat currentOutputFormat = OutputFormat.text;

        public static string ToolName { get => CliHelpers.GetToolName() ?? ""; }
        public static string ToolVersion { get => CliHelpers.GetToolVersion() ?? ""; }

        public BaseTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }
        
        public BaseTool() : base()
        {}

        public abstract Task<ErrorCode> RunAsync(T opt);

        /// <summary>
        /// Formulates the help text for each derived tool
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        /// <param name="errs"></param>
        protected void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            HelpText helpText = HelpText.AutoBuild(result, h =>
            {
                h.AddDashesToOption = true;
                h.AutoVersion = true;
                h.AdditionalNewLineAfterOption = false;
                h.MaximumDisplayWidth = Console.WindowWidth;
                h.AddPostOptionsLines(CliHelpers.GetCommonSupportedHelpTextLines());
                return HelpText.DefaultParsingErrorsHandler(result, h);
            });
            Console.Error.Write(helpText);
        }

        /// <summary>
        /// Use the CommandlineParser library to get the cmd line arguments
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="args"></param>
        /// <returns>The Action object with the parsed options</returns>
        protected ParserResult<T> ParseOptions<T>(string[]? args)
        {
            Parser parser = new();
            ParserResult<T> parserResult = parser.ParseArguments<T>(args);
            parserResult.WithNotParsed(errs => DisplayHelp(parserResult, errs));
            return parserResult;
        }

        /// <summary>
        /// Restores the output stream to Console, if it was changed to something else.
        /// </summary>
        protected void RestoreOutput()
        {
            if (redirectConsole)
            {
                ConsoleHelper.RestoreConsole();
            }
        }

        /// <summary>
        /// Use the OutputBuilder to select the given format and return a output builder. The format
        /// should be compatible with one of the enum entries in OutputFormat text format will be
        /// chosen, if the format is invalid.
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        protected IOutputBuilder SelectFormat(string format)
        {
            try
            {
                currentOutputFormat = GetOutputFormat(format);
                return CreateOutputBuilder(currentOutputFormat);
            }
            catch (ArgumentOutOfRangeException)
            {
                Logger.Debug("Invalid output format, selecting text");
            }

            currentOutputFormat = OutputFormat.text;
            return CreateDefaultOutputBuilder();
        }

        /// <summary>
        /// Change the tool output from the existing one to the passed in file. If the outputFile is
        /// not a valid filename, the output will be switched to Console.
        /// </summary>
        /// <param name="outputFile"></param>
        protected void SelectOutput(string outputFile)
        {
            // If outputFile is valid, then redirect output there.
            if (string.IsNullOrWhiteSpace(outputFile) ||
                outputFile.IndexOfAny(Path.GetInvalidPathChars()) < 0 ||
                Path.GetFileName(outputFile).IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            {
                redirectConsole = true;
                if (!ConsoleHelper.RedirectConsole(outputFile))
                {
                    Logger.Debug("Could not switch output from console to file");
                    // continue with current output
                }
            }
            else
            {
                redirectConsole = false;
                Logger.Debug("Invalid file, {0}, writing to console instead.", outputFile);
            }
        }

        protected void ConfigureLogging(BaseToolOptions options)
        {
            var config = NLog.LogManager.Configuration;
            var rule = config.LoggingRules.FirstOrDefault();
            
            if (rule != null)
            {
                NLog.LogLevel? targetLevel = ParseLogLevel(options.LogLevel);

                if (targetLevel != null)
                {
                    rule.SetLoggingLevels(targetLevel, NLog.LogLevel.Fatal);
                    NLog.LogManager.ReconfigExistingLoggers();
                    Logger.Debug("Log level set to: {0}", targetLevel);
                }
                else
                {
                    Logger.Warn("Invalid log level '{0}'. Using default (Info). Valid values: Trace(0), Debug(1), Info(2), Warn(3), Error(4), Fatal(5), Off(6)", options.LogLevel);
                }
            }
        }

        /// <summary>
        /// Parses a log level from either a string name or numeric value.
        /// Supports NLog log levels: Trace, Debug, Info, Warn, Error, Fatal, Off (case-insensitive).
        /// Also supports numeric values: 0=Trace, 1=Debug, 2=Info, 3=Warn, 4=Error, 5=Fatal, 6=Off.
        /// </summary>
        /// <param name="logLevel">The log level as a string or number.</param>
        /// <returns>The corresponding NLog.LogLevel, or null if invalid.</returns>
        private NLog.LogLevel? ParseLogLevel(string logLevel)
        {
            if (string.IsNullOrWhiteSpace(logLevel))
            {
                return null;
            }

            // Try parsing as a number first
            if (int.TryParse(logLevel, out int numericLevel))
            {
                return numericLevel switch
                {
                    0 => NLog.LogLevel.Trace,
                    1 => NLog.LogLevel.Debug,
                    2 => NLog.LogLevel.Info,
                    3 => NLog.LogLevel.Warn,
                    4 => NLog.LogLevel.Error,
                    5 => NLog.LogLevel.Fatal,
                    6 => NLog.LogLevel.Off,
                    _ => null
                };
            }

            // Try parsing as a string (case-insensitive)
            return logLevel.ToLowerInvariant() switch
            {
                "trace" => NLog.LogLevel.Trace,
                "debug" => NLog.LogLevel.Debug,
                "info" => NLog.LogLevel.Info,
                "warn" or "warning" => NLog.LogLevel.Warn,
                "error" => NLog.LogLevel.Error,
                "fatal" => NLog.LogLevel.Fatal,
                "off" or "none" => NLog.LogLevel.Off,
                _ => null
            };
        }

        private bool redirectConsole = false;
    }
}
