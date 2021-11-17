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
using Microsoft.CST.OpenSource.FindSquats.ExtensionMethods;
using Microsoft.CST.OpenSource.FindSquats.Mutators;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class FindSquatsTool : OSSGadget
    {
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
            MutateOptions checkerOptions = new MutateOptions()
            {
                SleepDelay = options.SleepDelay
            };
            foreach (var target in options.Targets ?? Array.Empty<string>())
            {
                var purl = new PackageURL(target);
                if (purl.Name is null || purl.Type is null)
                {
                    Logger.Trace($"Could not generate valid PackageURL from { target }.");
                    continue;
                }

                var manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                if (manager is null)
                {
                    Logger.Trace($"Could not generate valid ProjectManager from { purl }.");
                    continue;
                }

                await foreach (var potentialSquat in manager.EnumerateSquats(purl, checkerOptions))
                {
                    foundSquats++;
                    if (!options.Quiet)
                    {
                        Logger.Info($"{potentialSquat.PackageName} package exists. Potential squat. {JsonConvert.SerializeObject(potentialSquat.Rules)}");
                    }
                    if (outputBuilder is SarifOutputBuilder sarob)
                    {
                        SarifResult sarifResult = new SarifResult()
                        {
                            Message = new Message()
                            {
                                Text = $"Potential Squat candidate { potentialSquat.PackageName }.",
                                Id = "oss-find-squats"
                            },
                            Kind = ResultKind.Review,
                            Level = FailureLevel.None,
                            Locations = SarifOutputBuilder.BuildPurlLocation(potentialSquat.PackageUrl),
                        };
                        foreach (var tag in potentialSquat.Rules)
                        {
                            sarifResult.Tags.Add(tag);
                        }
                        sarob.AppendOutput(new SarifResult[] { sarifResult });
                    }
                    else if (outputBuilder is StringOutputBuilder strob)
                    {
                        var rulesString = string.Join(',', potentialSquat.Rules);
                        strob.AppendOutput(new string[] { $"Potential Squat candidate '{ potentialSquat.PackageName }' detected. Generated by { rulesString }.{Environment.NewLine}" });
                    }
                    else
                    {
                        var rulesString = string.Join(',', potentialSquat.Rules);
                        if (!options.Quiet)
                        {
                            Logger.Info($"Potential Squat candidate '{ potentialSquat.PackageName }' detected. Generated by { rulesString }.");
                        }
                    }
                }
            }

            return (outputBuilder.GetOutput(),foundSquats);
        }
    }
}
