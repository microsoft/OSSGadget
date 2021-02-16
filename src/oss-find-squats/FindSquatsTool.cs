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

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class FindSquatsTool : OSSGadget
    {
        /// <summary>
        ///     Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-detect-cryptography";

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

        public FindSquatsTool()
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
            var results = new List<string>();
            foreach(var target in opts.Targets ?? Array.Empty<string>())
            {
                var purl = new PackageURL(target);
                if (purl.Name is not null)
                {
                    var mutationsDict = gen.Mutate(purl.Name);
                    // Flatten results
                    var mutationsList = mutationsDict.SelectMany(x => x.Value);
                    foreach(var mutation in mutationsList)
                    {
                        switch (purl.Type)
                        {
                            case "npm":
                                if (await DoesCandidateExistInNpm(mutation) is string uri)
                                {
                                    results.Add(uri);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            var sarifResults = new List<Result>();
            foreach(var result in results)
            {
                Result res = new Result();
                Location loc = new Location();
                loc.LogicalLocation = new LogicalLocation()
                {
                    FullyQualifiedName = result
                };
                res.Locations.Add(loc);
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

        private async Task<string?> DoesCandidateExistInNpm(string mutation)
        {
            var output = new List<string>();
            var json = await client.GetAsync($"https://npmjs.com/search/suggestions?q={mutation}");
            var suggestions = JsonConvert.DeserializeObject<List<NpmSuggestion>>(await json.Content.ReadAsStringAsync());
            foreach(NpmSuggestion suggestion in suggestions)
            {
                var splits = suggestion.Name?.Split('/') ?? Array.Empty<string>();
                if (splits.Length == 2)
                {
                    if (splits[1].Equals(mutation, StringComparison.OrdinalIgnoreCase))
                    {
                        return suggestion.Links?["npm"];
                    }
                }
                else if (splits.Length == 1)
                {
                    if (splits[0].Equals(mutation, StringComparison.OrdinalIgnoreCase))
                    {
                        return suggestion.Links?["npm"];
                    }
                }
            }
            return null;
        }
    }
}
