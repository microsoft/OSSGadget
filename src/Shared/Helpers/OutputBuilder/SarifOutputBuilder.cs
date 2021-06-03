using Microsoft.CodeAnalysis.Sarif;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource.Shared
{
    public class SarifOutputBuilder : IOutputBuilder
    {
        public SarifOutputBuilder(SarifVersion version)
        {
            currentSarifVersion = version;
        }

        // default = text
        /// <summary>
        ///     Build a SARIF Result.Location object for the purl package
        /// </summary>
        /// <param name="purl"> </param>
        /// <returns> Location list with single location object </returns>
        /// <returns> Location list with single location object </returns>
        public static List<Location> BuildPurlLocation(PackageURL purl)
        {
            BaseProjectManager? projectManager = ProjectManagerFactory.CreateProjectManager(purl, null);
            if (projectManager == null)
            {
                Logger.Debug("Cannot determine the package type");
                return new List<Location>();
            }

            return new List<Location>() {
                new Location() {
                    PhysicalLocation = new PhysicalLocation()
                    {
                        Address = new Address()
                        {
                            FullyQualifiedName = projectManager.GetPackageAbsoluteUri(purl)?.AbsoluteUri,
                            AbsoluteAddress = PHYSICAL_ADDRESS_FLAG, // Sarif format needs non negative integer
                            Name = purl.ToString()
                        }
                    }
                }
            };
        }

        /// <summary> Adds the sarif results to the the Sarif Log. An incompatible object input will result in
        /// InvalidCast exception </summary> <param name="output">An IEnumerable<Result> object of sarif
        /// results </param>
        public void AppendOutput(IEnumerable<object> output)
        {
            sarifResults.AddRange((IEnumerable<SarifResult>)output);
        }

        /// <summary>
        ///     Builds a SARIF log object with the stored results
        /// </summary>
        /// <returns> </returns>
        public SarifLog BuildSingleRunSarifLog()
        {
            Tool thisTool = new Tool
            {
                Driver = new ToolComponent
                {
                    Name = AssemblyName,
                    Version = Version,
                    Organization = Company,
                    Product = "OSSGadget (https://github.com/Microsoft/OSSGadget)",
                }
            };

            SarifLog sarifLog = new SarifLog()
            {
                Runs = new List<Run>() {
                    new Run()
                    {
                        Tool = thisTool,
                        Results = sarifResults
                    }
                },
                Version = this.currentSarifVersion
            };
            return sarifLog;
        }

        /// <summary>
        ///     Gets the string representation of the sarif
        /// </summary>
        /// <returns> </returns>
        public string GetOutput()
        {
            SarifLog completedSarif = BuildSingleRunSarifLog();

            using (MemoryStream ms = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(ms, System.Text.Encoding.UTF8, -1, true);
                StreamReader sr = new StreamReader(ms);

                completedSarif.Save(sw);
                ms.Position = 0;

                return sr.ReadToEnd();
            }
        }

        /// <summary>
        ///     Prints to the sarif output as string
        /// </summary>
        public void PrintOutput()
        {
            PrintSarifLog(ConsoleHelper.GetCurrentWriteStream());
        }

        /// <summary>
        ///     Write the output to the given file. Creating directory if needed.
        /// </summary>
        public void WriteOutput(string fileName)
        {
            using var fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            using var sw = new StreamWriter(fs);
            PrintSarifLog(sw);
        }

        /// <summary>
        ///     Print the whole SARIF log to the stream
        /// </summary>
        /// <param name="writeStream"> </param>
        public void PrintSarifLog(StreamWriter writeStream)
        {
            SarifLog completedSarif = BuildSingleRunSarifLog();
            completedSarif.Save(writeStream);
        }

        /// <summary>
        ///     Class logger
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int PHYSICAL_ADDRESS_FLAG = 1;

        // cache variables to avoid reflection
        private static readonly string AssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

        private static readonly string Company = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
        private static readonly string Version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version.ToString() ?? string.Empty;
        private readonly SarifVersion currentSarifVersion = SarifVersion.Current;
        private List<Result> sarifResults = new List<Result>();
    }
}