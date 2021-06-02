using CommandLine;
using CommandLine.Text;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class FindSquatsTool : OSSGadget
    {
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

            [Option('q', "quiet", Required = false, Default = false,
                HelpText = "Suppress console output.")]
            public bool Quiet { get; set; } = false;

            [Option('s', "sleep-delay", Required = false, Default = 0, HelpText = "Number of ms to sleep between checks.")]
            public int SleepDelay { get; set; }

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
            ShowToolBanner();
            var findSquatsTool = new FindSquatsTool();
            (string output, int numSquats) = (string.Empty, 0);
            findSquatsTool.ParseOptions<Options>(args).WithParsed<Options>(options =>
            {
                (output, numSquats) = findSquatsTool.RunAsync(options).Result;
                if (string.IsNullOrEmpty(options.OutputFile))
                {
                    options.OutputFile = $"oss-find-squat.{options.Format}";
                }
                if (!options.Quiet)
                {
                    if (numSquats > 0)
                    {
                        Logger.Warn($"Found {numSquats} potential squats.");
                    }
                    else
                    {
                        Logger.Info($"No squats detected.");
                    }
                }
                using var fw = new StreamWriter(options.OutputFile);
                fw.WriteLine(output);
                fw.Close();

            });
        }

        public async Task<(string output, int numSquats)> RunAsync(Options options)
        {
            IOutputBuilder outputBuilder = SelectFormat(options.Format);
            var foundSquats = 0;
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
                            if (options.SleepDelay > 0)
                            {
                                Thread.Sleep(options.SleepDelay);
                            }
                            // Nuget will match "microsoft.cst.oat." against "Microsoft.CST.OAT" but these are the same package
                            // For nuget in particular we filter out this case
                            if (manager is NuGetProjectManager)
                            {
                                if (candidate.EndsWith('.'))
                                {
                                    if (candidate.Equals($"{purl.Name}.", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        continue;
                                    }
                                }
                            }
                            var candidatePurl = new PackageURL(purl.Type, candidate);
                            try
                            {
                                var versions = await manager.EnumerateVersions(candidatePurl);

                                if (versions.Any())
                                {
                                    foundSquats++;
                                    if (!options.Quiet)
                                    {
                                        Logger.Info($"{candidate} package exists. Potential squat. {JsonConvert.SerializeObject(rules)}");
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
                                            Locations = SarifOutputBuilder.BuildPurlLocation(candidatePurl),
                                        };
                                        foreach(var tag in rules)
                                        {
                                            sarifResult.Tags.Add(tag);
                                        }
                                        sarob.AppendOutput(new SarifResult[] { sarifResult });
                                    }
                                    else if (outputBuilder is StringOutputBuilder strob)
                                    {
                                        var rulesString = string.Join(',', rules);
                                        strob.AppendOutput(new string[] { $"Potential Squat candidate '{ candidate }' detected. Generated by { rulesString }.{Environment.NewLine}" });
                                    }
                                    else
                                    {
                                        var rulesString = string.Join(',', rules);
                                        if (!options.Quiet)
                                        {
                                            Logger.Info($"Potential Squat candidate '{ candidate }' detected. Generated by { rulesString }.");
                                        }
                                    }
                                }
                            }
                            catch (Exception e) 
                            {
                                Logger.Trace($"Could not enumerate versions. Package {candidate} likely doesn't exist. {e.Message}:{e.StackTrace}");
                            }
                        }
                    }
                }
            }

            return (outputBuilder.GetOutput(),foundSquats);
        }
    }
}
