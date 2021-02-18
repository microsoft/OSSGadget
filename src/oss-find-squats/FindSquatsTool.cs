using CommandLine;
using CommandLine.Text;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Baseline.ResultMatching;
using Microsoft.CST.OpenSource.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.FindSquats;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class FindSquatsTool : OSSGadget
    {
        /// <summary>
        ///     Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-find-squats";

        /// <summary>
        ///     Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(FindSquatsTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

        Generative gen { get; set; }

        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find Squat Candidates for the Given Packages",
                        new Options { Targets = new List<string>() {"[options]", "package-urls..." } })};
                }
            }

            [Option('o', "output", Required = true, HelpText = "The filename to output.")]
            public string? OutputFileName { get; set; }

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required)")] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }

        }

        public FindSquatsTool() : base()
        {
            gen = new Generative();
            client = new HttpClient();
        }

        HttpClient client;
        static async Task Main(string[] args)
        {
            var findSquatsTool = new FindSquatsTool();
            await findSquatsTool.ParseOptions<Options>(args).WithParsedAsync(findSquatsTool.RunAsync);
        }

        public async Task RunAsync(Options opts)
        {
            var results = new List<(string rule, PackageURL purl)>();
            foreach(var target in opts.Targets ?? Array.Empty<string>())
            {
                var purl = new PackageURL(target);
                if (purl.Name is not null && purl.Type is not null)
                {
                    var manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                    if (manager is not null)
                    {
                        var mutationsDict = gen.Mutate(purl.Name);

                        foreach((var rule, var list) in mutationsDict)
                        {
                            foreach (var mutation in list)
                            {
                                var innerPurl = new PackageURL(purl.Type, mutation);
                                var versions = await manager.EnumerateVersions(innerPurl);

                                if (versions.Any())
                                {
                                    results.Add((rule,purl));
                                }
                            }
                        }
                    }
                }
            }

            var sarifResults = new List<Result>();
            foreach(var result in results)
            {
                Result res = new Result();
                res.Locations = new List<Location>();
                Location loc = new Location();
                loc.LogicalLocation = new LogicalLocation()
                {
                    FullyQualifiedName = result.purl.ToString()
                };
                res.Locations.Add(loc);
                res.Message = new Message(result.rule, result.rule, result.rule, null, null);
                res.Kind = ResultKind.Review;
                sarifResults.Add(res);
            }

            SarifLog sarifLog = new SarifLog();
            sarifLog.Version = SarifVersion.Current;
            Run runItem = new Run();
            runItem.Tool = new Tool();

            runItem.Tool.Driver = new ToolComponent();
            if (Assembly.GetEntryAssembly() is Assembly entryAssembly)
            {
                runItem.Tool.Driver.Name = entryAssembly.GetName().Name;

                runItem.Tool.Driver.FullName = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>()?
                                                     .Product;

                runItem.Tool.Driver.Version = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                                    .InformationalVersion;
            }

            var descriptors = new List<ReportingDescriptor>();
            foreach (var mutation in gen.Mutations)
            {
                ReportingDescriptor sarifRule = new ReportingDescriptor();
                sarifRule.Name = mutation.Method.Name;
                sarifRule.DefaultConfiguration = new ReportingConfiguration() { Enabled = true };
            }

            runItem.Tool.Driver.Rules = descriptors;

            runItem.Results = sarifResults;

            sarifLog.Runs = new List<Run>();
            sarifLog.Runs.Add(runItem);
            sarifLog.Save(opts.OutputFileName);
        }
    }
}
