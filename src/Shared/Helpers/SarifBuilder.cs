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

            SarifLog sarifLog = new SarifLog()
            {
                Runs = new List<Run>() {
                    new Run()
                        {
                            Tool = thisTool,
                            Results = results
                        }
                }
            };

            return sarifLog;
        }

        public void PrintSarifLog(List<Result> results, StreamWriter writeStream)
        {
            SarifLog completedSarif = BuildSingleRunSarifLog(results);
            completedSarif.Save(writeStream);

        }
    }
}
