// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource.Health
{
    public class HealthTool
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-health";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(HealthTool).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; }

        /// <summary>
        /// Command line options
        /// </summary>
        private readonly Dictionary<string, object> Options = new Dictionary<string, object>()
        {
            { "target", new List<string>() }

        };


        static void Main(string[] args)
        {
            var healthTool = new HealthTool();
            Logger.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            healthTool.ParseOptions(args);

            if (((IList<string>)healthTool.Options["target"]).Count > 0)
            {
                foreach (var target in (IList<string>)healthTool.Options["target"])
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        CheckHealth(purl).Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger.Warn("No target provided; nothing to check health of.");
                HealthTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        public HealthTool()
        {
            CommonInitialization.Initialize();
            Logger = CommonInitialization.Logger;
        }
        
        public async static Task CheckHealth(PackageURL purl)
        {
            // Use reflection to find the correct package management class
            var projectManagerClass = typeof(BaseProjectManager).Assembly.GetTypes()
               .Where(type => type.IsSubclassOf(typeof(BaseProjectManager)))
               .Where(type => type.Name.Equals($"{purl.Type}ProjectManager",
                                               StringComparison.InvariantCultureIgnoreCase))
               .FirstOrDefault();

            if (projectManagerClass != null)
            {
                var ctor = projectManagerClass.GetConstructor(Array.Empty<Type>());
                var projectManager = (BaseProjectManager)(ctor.Invoke(Array.Empty<object>()));
                var content = await projectManager.GetMetadata(purl);
                foreach (var githubPurl in BaseProjectManager.ExtractGitHubPackageURLs(content))
                {
                    try
                    {
                        var healthAlgorithm = new GitHubHealthAlgorithm(githubPurl);
                        var health = await healthAlgorithm.GetHealth();
                        // @TODO Improve this output
                        Logger.Info($"Health for {purl} (via {githubPurl})");
                        Logger.Info(health.ToString());
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Unable to calculate health for {0}: {1}", githubPurl, ex.Message);
                    }
                }
            }
            else
            {
                throw new ArgumentException("Invalid Package URL type: {0}", purl.Type);
            }
        }

        /// <summary>
        /// Parses options for this program.
        /// </summary>
        /// <param name="args">arguments (passed in from the user)</param>
        private void ParseOptions(string[] args)
        {
            if (args == null)
            {
                ShowUsage();
                Environment.Exit(1);
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-h":
                    case "--help":
                        ShowUsage();
                        Environment.Exit(1);
                        break;

                    case "-v":
                    case "--version":
                        Console.Error.WriteLine($"{TOOL_NAME} {VERSION}");
                        Environment.Exit(1);
                        break;

                    default:
                        ((IList<string>)Options["target"]).Add(args[i]);
                        break;
                }
            }
        }

        /// <summary>
        /// Displays usage information for the program.
        /// </summary>
        private static void ShowUsage()
        {
            Console.Error.WriteLine($@"
{TOOL_NAME} {VERSION}

Usage: {TOOL_NAME} [options] package-url...

positional arguments:
    package-url                 PackgeURL specifier to download (required, repeats OK)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
