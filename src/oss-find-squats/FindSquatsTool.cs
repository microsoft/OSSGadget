using CommandLine;
using CommandLine.Text;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.FindSquats;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;
using Scriban.Runtime;
using System.IO;

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

            [Option('o', "output-file", Required = false, Default = "",
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; } = "";

            [Option('f', "format", Required = false, Default = "text",
                HelpText = "selct the output format(text|sarifv1|sarifv2)")]
            public string Format { get; set; } = "text";

            [Option('q', "quiet", Required = false, Default = "text",
                HelpText = "Suppress console output.")]
            public bool Quiet { get; set; } = false;

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
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

        public async Task RunAsync(Options options)
        {
            var results = new List<(IEnumerable<string> rule, PackageURL purl)>();
            IOutputBuilder outputBuilder = SelectFormat(options.Format);

            //SarifLog sarifLog = new SarifLog();
            //sarifLog.Version = SarifVersion.Current;
            //Run runItem = new Run();
            //runItem.Tool = new Tool();

            //runItem.Tool.Driver = new ToolComponent();
            //if (Assembly.GetEntryAssembly() is Assembly entryAssembly)
            //{
            //    runItem.Tool.Driver.Name = entryAssembly.GetName().Name;

            //    runItem.Tool.Driver.FullName = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>()?
            //                                         .Product;

            //    runItem.Tool.Driver.Version = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            //                                        .InformationalVersion;
            //}

            //var descriptors = new List<ReportingDescriptor>();

            //foreach (var mutation in gen.Mutations)
            //{
            //    ReportingDescriptor sarifRule = new ReportingDescriptor();
            //    sarifRule.Name = mutation.Method.Name;
            //    sarifRule.DefaultConfiguration = new ReportingConfiguration() { Enabled = true };
            //}

            //runItem.Tool.Driver.Rules = descriptors;

            foreach (var target in options.Targets ?? Array.Empty<string>())
            {
                var purl = new PackageURL(target);
                if (purl.Name is not null && purl.Type is not null)
                {
                    var manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                    if (manager is not null)
                    {
                        var mutationsDict = gen.Mutate(purl.Name);

                        foreach ((var candidate, var rules) in mutationsDict)
                        {
                            var candidatePurl = new PackageURL(purl.Type, candidate);
                            var versions = await manager.EnumerateVersions(candidatePurl);

                            if (versions.Any())
                            {
                                if (!options.Quiet)
                                {
                                    Logger.Info($"{candidate} package exists. Potential squat.");
                                }
                                if (outputBuilder is SarifOutputBuilder sarob)
                                {
                                    SarifResult sarifResult = new SarifResult()
                                    {
                                        Message = new Message()
                                        {
                                            Text = $"Potential Squat candidate { candidate }.",
                                            Id = "oss-find-squats"
                                        },
                                        Kind = ResultKind.Review,
                                        Level = FailureLevel.None,
                                        Locations = SarifOutputBuilder.BuildPurlLocation(purl),
                                    };
                                    sarob.AppendOutput(new SarifResult[] { sarifResult });
                                }
                                else if (outputBuilder is StringOutputBuilder strob)
                                {
                                    var rulesString = string.Join(',', rules);
                                    strob.AppendOutput(new string[] { $"Potential Squat candidate { candidate } detected. Generated by { rulesString }." });
                                }
                                else
                                {
                                    var rulesString = string.Join(',', rules);
                                    if (!options.Quiet)
                                    {
                                        Logger.Info($"Potential Squat candidate { candidate } detected. Generated by { rulesString }.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(options.OutputFile))
            {
                options.OutputFile = $"oss-find-squat.{options.Format}";
            }

            using var fw = new StreamWriter(options.OutputFile);
            fw.WriteLine(outputBuilder.GetOutput());
        }
    }
}
