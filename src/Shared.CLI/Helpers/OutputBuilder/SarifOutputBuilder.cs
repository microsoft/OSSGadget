// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Shared
{
    using Contracts;
    using Microsoft.CodeAnalysis.Sarif;
    using Microsoft.CST.OpenSource.PackageManagers;
    using Newtonsoft.Json;
    using PackageUrl;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

    public class SarifOutputBuilder : IOutputBuilder
    {
        public SarifOutputBuilder(SarifVersion version)
        {
            currentSarifVersion = version;
        }

        // default = text
        /// <summary>
        /// Build a SARIF Result.Location object for the purl package
        /// </summary>
        /// <param name="purl">The <see cref="PackageURL"/> to build the location for.</param>
        /// <returns>Location list with single location object</returns>
        public static List<Location> BuildPurlLocation(PackageURL purl)
        {
            IBaseProjectManager? projectManager = ProjectManagerFactory.ConstructPackageManager(purl, null);
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

        /// <summary> Adds the sarif results to the the Sarif Log. An incompatible object input will
        /// result in InvalidCast exception </summary> <param name="output">An IEnumerable<Result>
        /// object of sarif results </param>
        public void AppendOutput(IEnumerable<object> output)
        {
            sarifResults.AddRange((IEnumerable<SarifResult>)output);
        }

        /// <summary>
        /// Builds a SARIF log object with the stored results
        /// </summary>
        /// <returns></returns>
        public SarifLog BuildSingleRunSarifLog()
        {
            Tool thisTool = new()
            {
                Driver = new ToolComponent
                {
                    Name = AssemblyName,
                    Version = Version,
                    Organization = Company,
                    Product = "OSSGadget (https://github.com/Microsoft/OSSGadget)",
                }
            };

            SarifLog sarifLog = new()
            {
                Runs = new List<Run>() {
                    new Run()
                    {
                        Tool = thisTool,
                        Results = sarifResults
                    }
                },
                Version = currentSarifVersion
            };
            return sarifLog;
        }

        /// <summary>
        /// Gets the string representation of the sarif
        /// </summary>
        /// <returns></returns>
        public string GetOutput()
        {
            SarifLog completedSarif = BuildSingleRunSarifLog();

            using MemoryStream ms = new();
            StreamWriter sw = new(ms, System.Text.Encoding.UTF8, -1, true);
            StreamReader sr = new(ms);

            completedSarif.Save(sw);
            ms.Position = 0;

            return sr.ReadToEnd();
        }

        /// <summary>
        /// Prints to the sarif output as string
        /// </summary>
        public void PrintOutput()
        {
            PrintSarifLog(ConsoleHelper.GetCurrentWriteStream());
        }

        /// <summary>
        /// Write the output to the given file. Creating directory if needed.
        /// </summary>
        public void WriteOutput(string fileName)
        {
            using FileStream fs = new(fileName, FileMode.Create, FileAccess.ReadWrite);
            using StreamWriter sw = new(fs);
            PrintSarifLog(sw);
        }

        /// <summary>
        /// Print the whole SARIF log to the stream.
        /// </summary>
        /// <param name="writeStream">StreamWriter to write the SARIF result to.</param>
        public void PrintSarifLog(StreamWriter writeStream)
        {
            SarifLog completedSarif = BuildSingleRunSarifLog();

            JsonSerializer serializer = new() { Formatting = Formatting.Indented };
            using JsonTextWriter writer = new(writeStream);
            serializer.Serialize(writer, completedSarif);
        }

        /// <summary>
        /// Class logger
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int PHYSICAL_ADDRESS_FLAG = 1;

        // cache variables to avoid reflection
        private static readonly string AssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

        private static readonly string Company = "Microsoft Corporation";
        private static readonly string Version = OSSGadget.GetToolVersion();
        private readonly SarifVersion currentSarifVersion = SarifVersion.Current;
        private readonly List<Result> sarifResults = new();
    }
}