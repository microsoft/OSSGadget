using Microsoft.CodeAnalysis.Sarif;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Microsoft.CST.OpenSource.Shared
{
    public class SarifBuilder
    {
        /// <summary>
        /// Class logger
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        const string SARIF_V1_STRING = "sarifv1";
        const string SARIF_V2_STRING = "sarifv2";
        const string TEXT_STRING = "text"; // no sarif, just text

        readonly string sarifVersion = SARIF_V2_STRING; // default = v2

        public SarifBuilder(string sarifVersion)
        {
            this.sarifVersion = sarifVersion;
        }

        public SarifLog BuildSingleRunSarifLog(List<Result> results)
        {
            Tool thisTool = new Tool
            {
                Driver = new ToolComponent
                {
                    Name = Assembly.GetEntryAssembly().GetName().Name,
                    // FullName = toolAssembly.FullName,
                    Version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version.ToString(),
                    Organization = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyCompanyAttribute>().Company,
                    Product = "OSSGadget (https://github.com/Microsoft/OSSGadget)",
                }
            };

            SarifVersion version = this.sarifVersion == "sarifv1" ? SarifVersion.OneZeroZero : SarifVersion.Current;
            SarifLog sarifLog = new SarifLog()
            {
                Runs = new List<Run>() {
                    new Run()
                        {
                            Tool = thisTool,
                            Results = results
                        }
                },
                Version = version
            };

            return sarifLog;
        }

        public void PrintSarifLog(List<Result> results, StreamWriter writeStream)
        {
            SarifLog completedSarif = BuildSingleRunSarifLog(results);
            completedSarif.Save(writeStream);
        }

        public static bool isValidSarifVersion(string sarifVersion)
        {
            return (sarifVersion == SARIF_V1_STRING || sarifVersion == SARIF_V2_STRING);
        }

        public static bool isTextFormat(string textVersion)
        {
            return (textVersion == TEXT_STRING);
        }
    }
}
