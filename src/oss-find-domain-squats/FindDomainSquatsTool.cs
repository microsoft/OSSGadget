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
using System.Text.RegularExpressions;
using NLog;
using Microsoft.CST.OpenSource.FindSquats.Mutators;

namespace Microsoft.CST.OpenSource.DomainSquats
{
    public class FindDomainSquatsTool : OSSGadget
    {
        internal static IList<Mutator> BaseMutators { get; } = new List<Mutator>()
        {
            new AfterSeparatorMutator(),
            new AsciiHomoglyphMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new SeparatorMutator(),
            new SubstitutionMutator(),
            new SuffixMutator(),
            new SwapOrderOfLettersMutator(),
            new UnicodeHomoglyphMutator(),
            new VowelSwapMutator(),
        };

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
        }

        static async Task Main(string[] args)
        {
            ShowToolBanner();

            var findSquatsTool = new FindDomainSquatsTool();
            (string output, int numRegisteredSquats, int numUnregisteredSquats) = (string.Empty, 0, 0);
            await findSquatsTool.ParseOptions<Options>(args).WithParsedAsync<Options>(findSquatsTool.RunAsync);
        }

        private readonly Regex ValidDomainRegex = new("^[0-9a-z]+[0-9a-z\\-]*[0-9a-z]+$", RegexOptions.Compiled);

        public async Task<(string output, int registeredSquats, int unregisteredSquats)> RunAsync(Options options)
        {
            IOutputBuilder outputBuilder = SelectFormat(options.Format);
            var registeredSquats = new List<(string, KeyValuePair<string, IList<string>>)>();
            var unregisteredSquats = new List<(string, KeyValuePair<string, IList<string>>)>();
            var failedSquats = new List<(string, KeyValuePair<string, IList<string>>)>();
            var whois = new WhoisLookup();
            foreach (var target in options.Targets ?? Array.Empty<string>())
            {
                var splits = target.Split('.');
                var domain = splits[0];
                var potentials = new Dictionary<string, IList<string>>();
                foreach (var mutator in BaseMutators)
                {
                    foreach (var mutation in mutator.Generate(splits[0]))
                    {
                        if (potentials.ContainsKey(mutation.Name))
                        {
                            potentials[mutation.Name].Add(mutation.Reason);
                        }
                        else
                        {
                            potentials[mutation.Name] = new List<string>() { mutation.Reason };
                        }
                    }
                }

                foreach(var potential in potentials)
                {
                    await CheckPotential(potential);
                }

                async Task CheckPotential(KeyValuePair<string, IList<string>> potential, int retries = 0)
                {
                    // Not a valid domain
                    if (!ValidDomainRegex.IsMatch(potential.Key))
                    {
                        return;
                    }
                    if (Uri.IsWellFormedUriString(potential.Key, UriKind.Relative))
                    {
                        var url = string.Join('.',potential.Key, string.Join('.',splits[1..]));

                        try
                        {
                            Thread.Sleep(options.SleepDelay);
                            var response = await whois.LookupAsync(url);
                            if (response.Status == WhoisStatus.Found)
                            {
                                registeredSquats.Add((url, potential));
                            }
                            else if (response.Status == WhoisStatus.NotFound)
                            {
                                unregisteredSquats.Add((url, potential));
                            }
                            else
                            {
                                failedSquats.Add((url, potential));
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(e, $"{e.Message}:{e.StackTrace}");

                            if (retries++ < 5)
                            {
                                Thread.Sleep(1000);
                                await CheckPotential(potential, retries);
                            }
                            else
                            {
                                failedSquats.Add((url, potential));
                            }
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(options.OutputFile))
            {
                options.OutputFile = $"oss-find-domain-squat.{options.Format}";
            }
            if (!options.Unregistered)
            {
                if (registeredSquats.Any())
                {
                    Logger.Warn($"Found {registeredSquats.Count} registered potential squats.");
                }
                foreach (var potential in registeredSquats)
                {
                    var output = $"Registered: {potential.Item1} (rules: {string.Join(',', potential.Item2.Value)})";
                    if (!options.Quiet)
                    {
                        Logger.Info(output);
                    }
                    switch (outputBuilder)
                    {
                        case StringOutputBuilder s:
                            s.AppendOutput(new string[] { output });
                            break;
                        case SarifOutputBuilder sarif:
                            SarifResult sarifResult = new SarifResult()
                            {
                                Message = new Message()
                                {
                                    Text = $"Potential Squat candidate { potential.Item1 } is Registered.",
                                    Id = "oss-find-domain-squats"
                                },
                                Kind = ResultKind.Review,
                                Level = FailureLevel.None,
                            };
                            sarifResult.Tags.Add("Registered");
                            foreach (var tag in potential.Item2.Value)
                            {
                                sarifResult.Tags.Add(tag);
                            }
                            sarif.AppendOutput(new SarifResult[] { sarifResult });
                            break;
                    }
                }
            }
            if (!options.Registered)
            {
                if (unregisteredSquats.Any())
                {
                    Logger.Warn($"Found {unregisteredSquats.Count} unregistered potential squats.");
                    foreach (var potential in unregisteredSquats)
                    {
                        var output = $"Unregistered: {potential.Item1} (rules: {string.Join(',', potential.Item2.Value)})";
                        if (!options.Quiet)
                        {
                            Logger.Info(output);
                        }
                        switch (outputBuilder)
                        {
                            case StringOutputBuilder s:
                                s.AppendOutput(new string[] { output });
                                break;
                            case SarifOutputBuilder sarif:
                                SarifResult sarifResult = new SarifResult()
                                {
                                    Message = new Message()
                                    {
                                        Text = $"Potential Squat candidate { potential.Item1 } is Unregistered.",
                                        Id = "oss-find-domain-squats"
                                    },
                                    Kind = ResultKind.Review,
                                    Level = FailureLevel.None,
                                };
                                sarifResult.Tags.Add("Unregistered");
                                foreach (var tag in potential.Item2.Value)
                                {
                                    sarifResult.Tags.Add(tag);
                                }
                                sarif.AppendOutput(new SarifResult[] { sarifResult });
                                break;
                        }
                    }
                }
            }
            if (failedSquats.Any())
            {
                Logger.Error($"{failedSquats.Count} potential squats hit an exception when querying.  Try increasing the sleep setting and trying again or check these manually.");
                if (!options.Quiet)
                {
                    foreach (var fail in failedSquats)
                    {
                        Logger.Info($"Failed: {fail.Item1} (rules: {string.Join(',', fail.Item2.Value)})");
                    }
                }

            }
            

            using var fw = new StreamWriter(options.OutputFile);
            var outString = outputBuilder.GetOutput();
            fw.WriteLine(outString);
            fw.Close();
            return (outString, registeredSquats.Count, unregisteredSquats.Count);
        }
    }
}
