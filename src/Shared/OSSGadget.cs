using CommandLine;
using CommandLine.Text;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

namespace Microsoft.CST.OpenSource
{
    public class OSSGadget
    {
        public OutputFormat currentOutputFormat = OutputFormat.text;

        public OSSGadget()
        {
            CommonInitialization.Initialize();
        }

        /// <summary>
        ///     Logger for this class
        /// </summary>
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Formulates the help text for each derived tool
        /// </summary>
        /// <typeparam name="T"> </typeparam>
        /// <param name="result"> </param>
        /// <param name="errs"> </param>
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
            Console.Error.Write(helpText);
        }

        /// <summary>
        ///     Use the CommandlineParser library to get the cmd line arguments
        /// </summary>
        /// <typeparam name="T"> </typeparam>
        /// <param name="args"> </param>
        /// <returns> The Action object with the parsed options </returns>
        protected ParserResult<T> ParseOptions<T>(string[]? args)
        {
            var parser = new Parser();
            var parserResult = parser.ParseArguments<T>(args);
            parserResult.WithNotParsed(errs => DisplayHelp(parserResult, errs));
            return parserResult;
        }

        /// <summary>
        ///     Restores the output stream to Console, if it was changed to something else
        /// </summary>
        protected void RestoreOutput()
        {
            if (this.redirectConsole)
            {
                ConsoleHelper.RestoreConsole();
            }
        }

        /// <summary>
        ///     Use the OutputBuilder to select the given format and return a output builder The format should
        ///     be compatible with one of the enum entries in OutputFormat text format will be chosen, if the
        ///     format is invalid
        /// </summary>
        /// <param name="format"> </param>
        /// <returns> </returns>
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
        ///     Change the tool output from the existing one to the passed in file If the outputFile is not a
        ///     valid filename, the output will be switched to Console
        /// </summary>
        /// <param name="outputFile"> </param>
        protected void SelectOutput(string outputFile)
        {
            // output to console or file?
            this.redirectConsole = !string.IsNullOrEmpty(outputFile) &&
                outputFile.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            if (redirectConsole)
            {
                if (outputFile is string outputLoc)
                {
                    if (!ConsoleHelper.RedirectConsole(outputLoc))
                    {
                        Logger.Debug("Could not switch output from console to file");
                        // continue with current output
                    }
                }
                else
                {
                    Logger.Debug($"Invalid outputFile {outputFile}. Switching to console");
                }
            }
        }

        private bool redirectConsole = false;
    }
}