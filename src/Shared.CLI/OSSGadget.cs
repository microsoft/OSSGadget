// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.CST.OpenSource.Shared;
    using PackageManagers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

    public class OSSGadget : OssGadgetLib
    {
        public OutputFormat currentOutputFormat = OutputFormat.text;

        public static string ToolName { get => GetToolName() ?? ""; }
        public static string ToolVersion { get => GetToolVersion() ?? ""; }

        public OSSGadget(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }
        
        public OSSGadget() : base()
        {}

        /// <summary>
        /// Logger for this class
        /// </summary>
        public static new NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

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
                h.AddPostOptionsLines(GetCommonSupportedHelpTextLines());
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

        public static void ShowToolBanner()
        {
            Console.Error.WriteLine(OSSGadget.GetBanner());
            string? toolName = GetToolName();
            string? toolVersion = GetToolVersion();
            Console.Error.WriteLine($"OSS Gadget - {toolName} {toolVersion} - github.com/Microsoft/OSSGadget");
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

        private bool redirectConsole = false;

        private static string GetBanner()
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

        protected static string GetCommonSupportedHelpText()
        {
            string supportedHelpText = @"
The package-url specifier is described at https://github.com/package-url/purl-spec:
    pkg:cargo/rand                The latest version of Rand (via crates.io)
    pkg:cocoapods/AFNetworking    The latest version of AFNetworking (via cocoapods.org)
    pkg:composer/Smarty/Smarty    The latest version of Smarty (via Composer/ Packagist)
    pkg:cpan/Apache-ACEProxy      The latest version of Apache::ACEProxy (via cpan.org)
    pkg:cran/ACNE@0.8.0           Version 0.8.0 of ACNE (via cran.r-project.org)
    pkg:gem/rubytree@*            All versions of RubyTree (via rubygems.org)
    pkg:golang/sigs.k8s.io/yaml   The latest version of sigs.k8s.io/yaml (via proxy.golang.org)
    pkg:github/Microsoft/DevSkim  The latest release of DevSkim (via GitHub)
    pkg:hackage/a50@*             All versions of a50 (via hackage.haskell.org)
    pkg:maven/org.apdplat/deep-qa The latest version of org.apdplat.deep-qa (via repo1.maven.org)
    pkg:npm/express               The latest version of Express (via npmjs.com)
    pkg:nuget/Newtonsoft.JSON     The latest version of Newtonsoft.JSON (via nuget.org)
    pkg:pypi/django@1.11.1        Version 1.11.1 of Django (via pypi.org)
    pkg:ubuntu/zerofree           The latest version of zerofree from Ubuntu (via packages.ubuntu.com)
    pkg:vsm/MLNET/07              The latest version of MLNET.07 (from marketplace.visualstudio.com)
    pkg:url/foo@1.0?url=<URL>     The direct URL <URL>\n";
            return supportedHelpText;
        }

        private static List<string> GetCommonSupportedHelpTextLines()
        {
            return GetCommonSupportedHelpText().Split(Environment.NewLine).ToList<string>();
        }

        private static Regex detectUnencodedNamespace = new Regex("pkg:[^/]+/(@)[^/]+/[^/]+");
        /// <summary>
        /// This method converts an @ specified in a PackageURL namespace to %40 to comply with the PackageURL specification.
        /// This is only intended for use from CLI context where the input is provided by an interactive user to the application to reduce confusion.
        /// </summary>
        /// <returns>The PackageURL with @ converted to %40 if it appears in as the first character in the namespace specification.</returns>
        protected static string EscapeAtSymbolInNameSpace(string originalPackageUrlString)
        {
            MatchCollection matches = detectUnencodedNamespace.Matches(originalPackageUrlString);
            if (matches.Any())
            {
                var indexOfAt = matches.First().Groups[1].Index;
                return originalPackageUrlString[0..indexOfAt] + "%40" + originalPackageUrlString[(indexOfAt +1)..];
            }
            return originalPackageUrlString;
        }
    }
}
