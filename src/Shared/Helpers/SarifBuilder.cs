using Microsoft.CodeAnalysis.Sarif;
using NLog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Microsoft.CST.OpenSource
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

        public void WriteSarifLog(List<Result> results, string filePath)
        {
            SarifLog completedSarif = BuildSingleRunSarifLog(results);
            Logger.Debug($"Writing sarif to file {filePath}");
            try
            {
                completedSarif.Save(filePath);
            }
            catch(Exception ex)
            {
                Logger.Error($"Exception while saving sarif file {filePath}: {ex.ToString()}    ");
            }
        }
    }
}
