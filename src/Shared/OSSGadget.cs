using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using Microsoft.CST.OpenSource.Shared;
using static Microsoft.CST.OpenSource.Shared.OutputBuilder;

namespace Microsoft.CST.OpenSource
{
    public class OSSGadget
    {
        /// <summary>
        /// Logger for this class
        /// </summary>
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        bool redirectConsole = false;

        public OSSGadget()
        {
            CommonInitialization.Initialize();
        }

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

        protected ParserResult<T> ParseOptions<T>(string[]? args)
        {
            var parser = new Parser();
            var parserResult = parser.ParseArguments<T>(args);
            parserResult.WithNotParsed(errs => DisplayHelp(parserResult, errs));
            return parserResult;
        }

        protected void SelectOutput(string? outputFile)
        {
            // output to console or file?
            this.redirectConsole = !string.IsNullOrEmpty(outputFile);
            if (redirectConsole && outputFile is string outputLoc)
            {
                if (!ConsoleHelper.RedirectConsole(outputLoc))
                {
                    Logger.Error("Could not switch output from console to file");
                    // continue with current output
                }
            }
        }

        protected void RestoreOutput()
        {
            if (this.redirectConsole)
            {
                ConsoleHelper.RestoreConsole();
            }
        }

        protected OutputBuilder? SelectFormat(string? format)
        {
            try
            {
                return new OutputBuilder(format ?? OutputFormat.text.ToString());
            }
            catch (ArgumentOutOfRangeException)
            {
                Logger.Error("Invalid output format, selecting text");
            }

            return new OutputBuilder(OutputFormat.text.ToString());
        }
    }
}
