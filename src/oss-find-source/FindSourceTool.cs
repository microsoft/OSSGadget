// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class FindSourceTool
    {
        /// <summary>
        /// Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-find-source";

        /// <summary>
        /// Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(FindSourceTool).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Logger for this class
        /// </summary>
        private static NLog.ILogger Logger { get; set; }

        /// <summary>
        /// Command line options
        /// </summary>
        private readonly Dictionary<string, object> Options = new Dictionary<string, object>()
        {
            { "target", new List<string>() },
        };

        static void Main(string[] args)
        {
            var findSourceTool = new FindSourceTool();
            Logger.Debug($"Microsoft OSS Gadget - {TOOL_NAME} {VERSION}");
            findSourceTool.ParseOptions(args);

            if (((IList<string>)findSourceTool.Options["target"]).Count > 0)
            {
                foreach (var target in (IList<string>)findSourceTool.Options["target"])
                {
                    try
                    {
                        var purl = new PackageURL(target);
                        findSourceTool.FindSource(purl).Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
            else
            {
                Logger.Warn("No target provided; nothing to locate source for.");
                FindSourceTool.ShowUsage();
                Environment.Exit(1);
            }
        }

        public FindSourceTool()
        {
            CommonInitialization.Initialize();
            Logger = CommonInitialization.Logger;
        }

        public async Task FindSource(PackageURL purl)
        {
            var purlNoVersion = new PackageURL(purl.Type, purl.Namespace, purl.Name,
                                               null, purl.Qualifiers, purl.Subpath);
            Logger.Debug("Searching for source code for {0}", purlNoVersion.ToString());

            RepoSearch repoSearcher = new RepoSearch();
            Dictionary<PackageURL, float> repos = await repoSearcher.ResolvePackageLibraryAsync(purl);
            if (repos.Count > 0)
            {
                foreach (KeyValuePair<PackageURL, float> item in repos)
                {
                    var githubUrl = $"https://github.com/{item.Key.Namespace}/{item.Key.Name}";
                    Logger.Info("Found: {0} ({1}). Probability Score: {2}", item.Key.ToString(), githubUrl, item.Value);
                }
            }
            else
            {
                Logger.Warn("Could not find repository for package {0}", purl.ToString());
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

            if (((IList<string>)Options["target"]).Count == 0)
            {

                Logger.Error("Please enter the package(s) to search for");
                ShowUsage();
                Environment.Exit(1);
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
    package-url                 PackgeURL specifier to analyze (required, repeats OK)

{BaseProjectManager.GetCommonSupportedHelpText()}

optional arguments:
  --help                        show this help message and exit
  --version                     show version of this tool
");
        }
    }
}
