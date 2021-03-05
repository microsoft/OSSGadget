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
using Microsoft.CST.OpenSource.FindSquats;
using System.Net;
using Pastel;
using System.Drawing;
using System.Text;
using Whois;

namespace Microsoft.CST.OpenSource.DomainSquats
{
    public class FindDomainSquatsTool : OSSGadget
    {
        /// <summary>
        ///     Name of this tool.
        /// </summary>
        private const string TOOL_NAME = "oss-find-domain-squats";

        /// <summary>
        ///     Holds the version string, from the assembly.
        /// </summary>
        private static readonly string VERSION = typeof(FindSquats.FindSquatsTool).Assembly?.GetName().Version?.ToString() ?? string.Empty;

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
                            new Options { Targets = new List<string>() {"[options]", "domains" } })};
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

            [Option('u', "unregistered", Required = false, Default = false, HelpText = "Don't show registered domains.")]
            public bool Unregistered { get; set; }

            [Option('r', "registered", Required = false, Default = false, HelpText = "Don't show unregistered domains.")]
            public bool Registered { get; set; }

            [Value(0, Required = true,
                HelpText = "Domain(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }
        }

        public FindDomainSquatsTool() : base()
        {
            gen = new Generative();
        }

        HttpClient client;
        static async Task Main(string[] args)
        {
            var findSquatsTool = new FindDomainSquatsTool();
            (string output, int numRegisteredSquats, int numUnregisteredSquats) = (string.Empty, 0, 0);
            await findSquatsTool.ParseOptions<Options>(args).WithParsedAsync<Options>(findSquatsTool.RunAsync);
        }

        public async Task<(string output, int registeredSquats, int unregisteredSquats)> RunAsync(Options options)
        {
            IOutputBuilder outputBuilder = SelectFormat(options.Format);
            var registeredSquats = 0;
            var unregisteredSquats = 0;
            var whois = new WhoisLookup();
            foreach (var target in options.Targets ?? Array.Empty<string>())
            {
                var splits = target.Split('.');
                var potentials = gen.Mutate(splits[0]);
                foreach (var potential in potentials)
                {
                    if (Uri.IsWellFormedUriString(potential.Key, UriKind.Relative))
                    {
                        var urlBuilder = new StringBuilder();
                        urlBuilder.Append(potential.Key);
                        for (int i = 1; i < splits.Length; i++)
                        {
                            urlBuilder.Append($".{splits[i]}");
                        }

                        var url = urlBuilder.ToString();

                        try
                        {
                            Thread.Sleep(options.SleepDelay);
                            var response = await whois.LookupAsync(url);
                            if (response.Registered is DateTime)
                            {
                                registeredSquats++;

                                if (!options.Unregistered)
                                {
                                    Logger.Info($"Found {url} as a registered domain.".Pastel(Color.MediumAquamarine));
                                    switch (outputBuilder)
                                    {
                                        case StringOutputBuilder s:
                                            s.AppendOutput(new string[] { $"{url} is registered." });
                                            break;
                                        case SarifOutputBuilder sarif:
                                            SarifResult sarifResult = new SarifResult()
                                            {
                                                Message = new Message()
                                                {
                                                    Text = $"Potential Squat candidate { url }.",
                                                    Id = "oss-find-domain-squats"
                                                },
                                                Kind = ResultKind.Review,
                                                Level = FailureLevel.None,
                                            };
                                            foreach (var tag in potential.Value)
                                            {
                                                sarifResult.Tags.Add(tag);
                                            }
                                            sarif.AppendOutput(new SarifResult[] { sarifResult });
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                unregisteredSquats++;

                                if (!options.Registered)
                                {
                                    Logger.Info($"Found {url} as an unregistered domain.".Pastel(Color.LightGoldenrodYellow));

                                    switch (outputBuilder)
                                    {
                                        case StringOutputBuilder s:

                                            s.AppendOutput(new string[] { $"{url} is not registered." });
                                            break;
                                        case SarifOutputBuilder sarif:
                                            SarifResult sarifResult = new SarifResult()
                                            {
                                                Message = new Message()
                                                {
                                                    Text = $"Squat candidate { url } is not registered.",
                                                    Id = "oss-find-domain-squats"
                                                },
                                                Kind = ResultKind.Review,
                                                Level = FailureLevel.None,
                                            };
                                            foreach (var tag in potential.Value)
                                            {
                                                sarifResult.Tags.Add(tag);
                                            }
                                            sarif.AppendOutput(new SarifResult[] { sarifResult });
                                            break;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {

                            unregisteredSquats++;
                            if (!options.Registered)
                            {
                                Logger.Info($"Found {url} as an unregistered domain.".Pastel(Color.LightGoldenrodYellow));

                                switch (outputBuilder)
                                {
                                    case StringOutputBuilder s:
                                        s.AppendOutput(new string[] { $"{url} is not registered." });
                                        break;
                                    case SarifOutputBuilder sarif:
                                        SarifResult sarifResult = new SarifResult()
                                        {
                                            Message = new Message()
                                            {
                                                Text = $"Squat candidate { url } is not registered.",
                                                Id = "oss-find-domain-squats"
                                            },
                                            Kind = ResultKind.Review,
                                            Level = FailureLevel.None,
                                        };
                                        foreach (var tag in potential.Value)
                                        {
                                            sarifResult.Tags.Add(tag);
                                        }
                                        sarif.AppendOutput(new SarifResult[] { sarifResult });
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(options.OutputFile))
            {
                options.OutputFile = $"oss-find-domain-squat.{options.Format}";
            }
            if (!options.Quiet)
            {
                if (registeredSquats > 0 || unregisteredSquats > 0)
                {
                    Logger.Warn($"Found {registeredSquats} registered potential squats amd {unregisteredSquats} unregistered potential squats.");
                }
                else
                {
                    Logger.Info($"No squats detected.");
                }
            }
            using var fw = new StreamWriter(options.OutputFile);
            var outString = outputBuilder.GetOutput();
            fw.WriteLine();
            fw.Close();
            return (outString, registeredSquats, unregisteredSquats);
        }
    }
}
