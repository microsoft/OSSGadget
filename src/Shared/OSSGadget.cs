// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.CST.OpenSource.Shared;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

    public class OSSGadget
    {
        public OutputFormat currentOutputFormat = OutputFormat.text;

        public static string ToolName { get => GetToolName() ?? ""; }
        public static string ToolVersion { get => GetToolVersion() ?? ""; }

        public OSSGadget()
        {
            CommonInitialization.Initialize();
        }

        /// <summary>
        /// Logger for this class
        /// </summary>
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

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
                h.AddPostOptionsLines(BaseProjectManager.GetCommonSupportedHelpTextLines());
                return HelpText.DefaultParsingErrorsHandler(result, h);
            });
            Console.Write(helpText);
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

        public static void ShowToolBanner()
        {
            Console.WriteLine(OSSGadget.GetBanner());
            string? toolName = GetToolName();
            string? toolVersion = GetToolVersion();
            Console.WriteLine($"OSS Gadget - {toolName} {toolVersion} - github.com/Microsoft/OSSGadget");
        }

        /// <summary>
        /// Calculates the tool name from the entry assembly.
        /// </summary>
        /// <returns></returns>
        public static string? GetToolName()
        {
            string? entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (entryAssembly != null)
            {
                return Path.GetFileNameWithoutExtension(entryAssembly) ?? "Unknown";
            }
            return "Unknown";
        }

        /// <summary>
        /// Calculates the tool version from the executing assembly.
        /// </summary>
        /// <returns></returns>
        public static string GetToolVersion()
        {
            Assembly? assembly = Assembly.GetExecutingAssembly();
            AssemblyInformationalVersionAttribute[]? versionAttributes = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false) as AssemblyInformationalVersionAttribute[];
            string? version = versionAttributes?[0].InformationalVersion;
            return version ?? "Unknown";
        }

        public static string GetBanner()
        {
            return @"
   ____   _____ _____    _____           _            _
  / __ \ / ____/ ____|  / ____|         | |          | |
 | |  | | (___| (___   | |  __  __ _  __| | __ _  ___| |_
 | |  | |\___ \\___ \  | | |_ |/ _` |/ _` |/ _` |/ _ \ __|
 | |__| |____) |___) | | |__| | (_| | (_| | (_| |  __/ |_
  \____/|_____/_____/   \_____|\__,_|\__,_|\__, |\___|\__|
                                            __/ |
                                           |___/          ";
        }

        private bool redirectConsole = false;
    }
}