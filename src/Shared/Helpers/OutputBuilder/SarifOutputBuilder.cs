using Microsoft.CodeAnalysis.Sarif;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource.Shared
{
    public class SarifOutputBuilder : IOutputBuilder
    {
        /// <summary>
        /// Class logger
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        List<Result>? sarifResults = new List<Result>();
        readonly SarifVersion currentSarifVersion = SarifVersion.Current; // default = text

        // cache variables to avoid reflection
        static readonly string AssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
        static readonly string Version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version.ToString() ?? string.Empty;
        static readonly string Company = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

        public SarifOutputBuilder(SarifVersion version)
        {
            this.currentSarifVersion = version;
        }

        /// <summary>
        /// Prints to the sarif output as string
        /// </summary>
        public void PrintOutput()
        {
            this.PrintSarifLog(ConsoleHelper.GetCurrentWriteStream());
        }

        /// <summary>
        /// Append results to sarif
        /// </summary>
        /// <param name="output"></param>
        public void AppendOutput(IEnumerable<object>? output)
        {
            var results = (IEnumerable<SarifResult>?)output ?? Array.Empty<SarifResult>().ToList();
            this.sarifResults?.AddRange(results);
        }

        /// <summary>
        /// Build a SARIF Result.Location object for the purl package
        /// </summary>
        /// <param name="purl"></param>
        /// <returns>Location list with single location object</returns>
        public static List<Location> BuildPurlLocation(PackageURL purl)
        {
            BaseProjectManager? projectManager = ProjectManagerFactory.CreateProjectManager(purl, null);
            if (projectManager == null)
            {
                Logger?.Error("Cannot determine the package type");
            }

            return new List<Location>() {
                new Location() {
                    PhysicalLocation = new PhysicalLocation()
                    {
                        Address = new Address()
                        {
                            FullyQualifiedName = projectManager?.GetPackageAbsoluteUri(purl)?.AbsoluteUri,
                            AbsoluteAddress = 1,
                            Name = purl.ToString()
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Builds a SARIF log object with the stored results
        /// </summary>
        /// <returns></returns>
        public SarifLog? BuildSingleRunSarifLog()
        {
            Tool thisTool = new Tool
            {
                Driver = new ToolComponent
                {
                    Name = AssemblyName,
                    // FullName = toolAssembly.FullName,
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
        /// Gets the string representation of the sarif
        /// </summary>
        /// <returns></returns>
        public string? GetOutput()
        {
            SarifLog? completedSarif = BuildSingleRunSarifLog();
            using (MemoryStream? ms = new MemoryStream())
            {
                StreamWriter? sw = new StreamWriter(ms, System.Text.Encoding.UTF8, -1, true);
                StreamReader? sr = new StreamReader(ms);

                completedSarif?.Save(sw);
                ms.Position = 0;

                string text = sr.ReadToEnd();
                sw?.Dispose();
                sr?.Dispose();
                return text;
            }
        }        

        /// <summary>
        /// Print the whole SARIF log to the stream
        /// </summary>
        /// <param name="writeStream"></param>
        public void PrintSarifLog(StreamWriter writeStream)
        {
            SarifLog? completedSarif = BuildSingleRunSarifLog();
            completedSarif?.Save(writeStream);
        }
    }
}
