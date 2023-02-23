// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.DomainSquats
{
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.CodeAnalysis.Sarif;
    using Microsoft.CST.OpenSource.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;
    using System.IO;
    using System.Threading;
    using Whois;
    using System.Text.RegularExpressions;
    using Microsoft.CST.OpenSource.FindSquats.Mutators;
    using PackageManagers;
    using System.Net.Http;

    public class FindDomainSquatsTool : OSSGadget
    {
        internal static IList<IMutator> BaseMutators { get; } = new List<IMutator>()
        {
            new AsciiHomoglyphMutator(),
            new BitFlipMutator(),
            new CloseLettersMutator(),
            new DoubleHitMutator(),
            new DuplicatorMutator(),
            new PrefixMutator(),
            new RemovedCharacterMutator(),
            new RemoveSeparatedSectionMutator(),
            new SeparatorChangedMutator(),
            new SeparatorRemovedMutator(),
            new SubstitutionMutator(),
            new SuffixMutator(),
            new SwapOrderOfLettersMutator(),
            new UnicodeHomoglyphMutator(),
            new VowelSwapMutator()
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
                HelpText = "specify the output format(text|sarifv1|sarifv2)")]
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

        public FindDomainSquatsTool(IHttpClientFactory httpClientFactory) : base(new ProjectManagerFactory(httpClientFactory))
        {
        }

        public FindDomainSquatsTool() : this(new DefaultHttpClientFactory()) { }

        static async Task Main(string[] args)
        {
            ShowToolBanner();
            FindDomainSquatsTool findSquatsTool = new();
            (string output, int numRegisteredSquats, int numUnregisteredSquats) = (string.Empty, 0, 0);
            await findSquatsTool.ParseOptions<Options>(args).WithParsedAsync<Options>(findSquatsTool.RunAsync);
        }

        private readonly Regex ValidDomainRegex = new("^[0-9a-z]+[0-9a-z\\-]*[0-9a-z]+$", RegexOptions.Compiled);

        public async Task<(string output, int registeredSquats, int unregisteredSquats)> RunAsync(Options options)
        {
            IOutputBuilder outputBuilder = SelectFormat(options.Format);
            List<(string, KeyValuePair<string, List<Mutation>>)> registeredSquats = new();
            List<(string, KeyValuePair<string, List<Mutation>>)> unregisteredSquats = new();
            List<(string, KeyValuePair<string, List<Mutation>>)> failedSquats = new();
            WhoisLookup whois = new();
            foreach (string? target in options.Targets ?? Array.Empty<string>())
            {
                string[] splits = target.Split('.');
                string domain = splits[0];
                List<Mutation> potentials = new();
                foreach (IMutator mutator in BaseMutators)
                {
                    foreach (Mutation mutation in mutator.Generate(splits[0]))
                    {
                        potentials.Add(mutation);
                    }
                }

                foreach(KeyValuePair<string, List<Mutation>> potential in potentials.GroupBy(x => x.Mutated).ToDictionary(x => x.Key, x => x.ToList()))
                {
                    await CheckPotential(potential);
                }

                async Task CheckPotential(KeyValuePair<string, List<Mutation>> potential, int retries = 0)
                {
                    // Not a valid domain
                    if (!ValidDomainRegex.IsMatch(potential.Key))
                    {
                        return;
                    }
                    if (Uri.IsWellFormedUriString(potential.Key, UriKind.Relative))
                    {
                        string url = string.Join('.',potential.Key, string.Join('.',splits[1..]));

                        try
                        {
                            Thread.Sleep(options.SleepDelay);
                            WhoisResponse response = await whois.LookupAsync(url);
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
                foreach ((string, KeyValuePair<string, List<Mutation>>) potential in registeredSquats)
                {
                    string output = $"Registered: {potential.Item1} (rules: {string.Join(',', potential.Item2.Value)})";
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
                            SarifResult sarifResult = new()
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
                            foreach (Mutation? tag in potential.Item2.Value)
                            {
                                sarifResult.Tags.Add(tag.Reason);
                                sarifResult.Tags.Add(tag.Mutator.ToString());
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
                    foreach ((string, KeyValuePair<string, List<Mutation>>) potential in unregisteredSquats)
                    {
                        string output = $"Unregistered: {potential.Item1} (rules: {string.Join(',', potential.Item2.Value)})";
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
                                SarifResult sarifResult = new()
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
                                foreach (Mutation? tag in potential.Item2.Value)
                                {
                                    sarifResult.Tags.Add(tag.Reason);
                                    sarifResult.Tags.Add(tag.Mutator.ToString());
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
                    foreach ((string, KeyValuePair<string, List<Mutation>>) fail in failedSquats)
                    {
                        Logger.Info($"Failed: {fail.Item1} (rules: {string.Join(',', fail.Item2.Value)})");
                    }
                }

            }
            

            using StreamWriter fw = new(options.OutputFile);
            string outString = outputBuilder.GetOutput();
            fw.WriteLine(outString);
            fw.Close();
            return (outString, registeredSquats.Count, unregisteredSquats.Count);
        }
    }
}
