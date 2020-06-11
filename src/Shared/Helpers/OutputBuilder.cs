﻿using Microsoft.CodeAnalysis.Sarif;
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
        #region Protected Fields

        /// <summary>
        /// Class logger
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        #endregion Protected Fields

        #region Private Fields

        // cache variables to avoid reflection
        private static readonly string AssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

        private static readonly string Company = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

        private static readonly string Version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version.ToString() ?? string.Empty;

        private readonly OutputFormat CurrentOutputFormat = OutputFormat.text;

        private List<Result> sarifResults = new List<Result>();

        private StringBuilder stringResults = new StringBuilder();

        #endregion Private Fields

        #region Public Constructors

        // default = text
        public OutputBuilder(string format)
        {
            if (!Enum.TryParse<OutputFormat>(format, true, out this.CurrentOutputFormat))
            {
                throw new ArgumentOutOfRangeException("Invalid output format");
            }
        }

        #endregion Public Constructors

        #region Public Enums

        public enum OutputFormat
        {
            sarifv1,
            sarifv2,
            text // no sarif, just text
        };

        #endregion Public Enums

        #region Public Methods

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
                Logger.Error("Cannot determine the package type");
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

        public bool isSarifFormat()
        {
            return this.CurrentOutputFormat == OutputFormat.sarifv1 ||
                this.CurrentOutputFormat == OutputFormat.sarifv2;
        }

        public bool isTextFormat()
        {
            return this.CurrentOutputFormat == OutputFormat.text;
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
        /// Print the whole SARIF log to the stream
        /// </summary>
        /// <param name="writeStream"></param>
        public void PrintSarifLog(StreamWriter writeStream)
        {
            SarifLog completedSarif = BuildSingleRunSarifLog();
            completedSarif.Save(writeStream);
        }

        #endregion Public Methods
    }
}