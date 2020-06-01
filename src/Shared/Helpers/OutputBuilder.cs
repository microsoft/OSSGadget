using Microsoft.CodeAnalysis.Sarif;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    /// <summary>
    /// Builds the output text based on the format specified
    /// </summary>
    public class OutputBuilder
    {
        /// <summary>
        /// Class logger
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        enum OutputFormat
        {
            sarifv1,
            sarifv2 ,
            text // no sarif, just text
        };

        readonly OutputFormat CurrentOutputFormat = OutputFormat.text; // default = text

        StringBuilder stringResults = new StringBuilder();
        List<Result> sarifResults = new List<Result>();

        // cache variables to avoid reflection
        static string _AssemblyName = null;
        string AssemblyName { 
            get
            {
                _AssemblyName ??= Assembly.GetEntryAssembly().GetName().Name;
                return _AssemblyName;
            } 
        }

        static string _Version = null;
        string Version
        {
            get
            {
                _Version ??= Assembly.GetEntryAssembly().
                    GetCustomAttribute<AssemblyFileVersionAttribute>().Version.ToString();
                return _Version;
            }
        }

        static string _Company = null;
        string Company
        {
            get
            {
                _Company ??= Assembly.GetEntryAssembly().
                    GetCustomAttribute<AssemblyCompanyAttribute>().Company;
                return _Company;
            }
        }

        public OutputBuilder(string format)
        {
            if(!Enum.TryParse<OutputFormat>(format, true, out this.CurrentOutputFormat))
            {
                throw new ArgumentOutOfRangeException("Invalid output format");
            }
        }

        /// <summary>
        /// Prints to the currently selected output
        /// </summary>
        public void PrintOutput()
        {
            if (this.CurrentOutputFormat == OutputFormat.text)
            {
                Console.Out.Write(this.stringResults.ToString());
            }
            else
            {
                this.PrintSarifLog(ConsoleHelper.GetCurrentWriteStream());
            }
        }

        /// <summary>
        /// Overload of AppendOutput to add to text
        /// </summary>
        /// <param name="output"></param>
        public void AppendOutput(string output)
        {
            this.stringResults.Append(output);
        }

        /// <summary>
        /// Overload of AppendOutput to add to SARIF
        /// </summary>
        /// <param name="results"></param>
        public void AppendOutput(List<Result> results)
        {
            this.sarifResults.AddRange(results);
        }

        /// <summary>
        /// Build a SARIF Result.Location object for the purl package
        /// </summary>
        /// <param name="purl"></param>
        /// <returns>Location list with single location object</returns>
        public static List<Location> BuildPurlLocation(PackageURL purl)
        {
            var projectManager = ProjectManagerFactory.CreateProjectManager(purl, null);

            if (projectManager == null)
            {
                Logger.Error("Cannot determine the package type");
            }

            return new List<Location>() {
                new Location() {
                    PhysicalLocation = new PhysicalLocation()
                    {
                        Address = new Address()
                        {
                            FullyQualifiedName = projectManager.GetPackageAbsoluteUri(purl).AbsoluteUri,
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
        public SarifLog BuildSingleRunSarifLog()
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

            SarifVersion version = this.CurrentOutputFormat == OutputFormat.sarifv1 ? 
                SarifVersion.OneZeroZero : SarifVersion.Current;
            SarifLog sarifLog = new SarifLog()
            {
                Runs = new List<Run>() {
                    new Run()
                    {
                        Tool = thisTool,
                        Results = sarifResults
                    }
                },
                Version = version
            };
            return sarifLog;
        }

        /// <summary>
        /// Print the whole SARIF log to the stream
        /// </summary>
        /// <param name="writeStream"></param>
        public void PrintSarifLog(StreamWriter writeStream)
        {
            SarifLog completedSarif = BuildSingleRunSarifLog();
            completedSarif.Save(writeStream);
        }

        public bool isTextFormat()
        {
            return this.CurrentOutputFormat == OutputFormat.text;
        }

        public bool isSarifFormat()
        {
            return this.CurrentOutputFormat == OutputFormat.sarifv1 ||
                this.CurrentOutputFormat == OutputFormat.sarifv2;
        }
    }
}
