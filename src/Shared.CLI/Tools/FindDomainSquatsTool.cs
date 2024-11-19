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
    using OssGadget.Options;
    using PackageManagers;
    using System.Net.Http;

    public class FindDomainSquatsTool : BaseTool<FindDomainSquatsToolOptions>
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
        
        public FindDomainSquatsTool(IHttpClientFactory httpClientFactory) : base(new ProjectManagerFactory(httpClientFactory))
        {
        }

        public FindDomainSquatsTool() : this(new DefaultHttpClientFactory()) { }

        private readonly Regex ValidDomainRegex = new("^[0-9a-z]+[0-9a-z\\-]*[0-9a-z]+$", RegexOptions.Compiled);

        public override async Task<ErrorCode> RunAsync(FindDomainSquatsToolOptions FindDomainSquatsToolOptions)
        {
            IOutputBuilder outputBuilder = SelectFormat(FindDomainSquatsToolOptions.Format);
            List<(string, KeyValuePair<string, List<Mutation>>)> registeredSquats = new();
            List<(string, KeyValuePair<string, List<Mutation>>)> unregisteredSquats = new();
            List<(string, KeyValuePair<string, List<Mutation>>)> failedSquats = new();
            WhoisLookup whois = new();
            foreach (string? target in FindDomainSquatsToolOptions.Targets ?? Array.Empty<string>())
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
                            Thread.Sleep(FindDomainSquatsToolOptions.SleepDelay);
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
            if (string.IsNullOrEmpty(FindDomainSquatsToolOptions.OutputFile))
            {
                FindDomainSquatsToolOptions.OutputFile = $"oss-find-domain-squat.{FindDomainSquatsToolOptions.Format}";
            }
            if (!FindDomainSquatsToolOptions.Unregistered)
            {
                if (registeredSquats.Any())
                {
                    Logger.Warn($"Found {registeredSquats.Count} registered potential squats.");
                }
                foreach ((string, KeyValuePair<string, List<Mutation>>) potential in registeredSquats)
                {
                    string output = $"Registered: {potential.Item1} (rules: {string.Join(',', potential.Item2.Value)})";
                    if (!FindDomainSquatsToolOptions.Quiet)
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
            if (!FindDomainSquatsToolOptions.Registered)
            {
                if (unregisteredSquats.Any())
                {
                    Logger.Warn($"Found {unregisteredSquats.Count} unregistered potential squats.");
                    foreach ((string, KeyValuePair<string, List<Mutation>>) potential in unregisteredSquats)
                    {
                        string output = $"Unregistered: {potential.Item1} (rules: {string.Join(',', potential.Item2.Value)})";
                        if (!FindDomainSquatsToolOptions.Quiet)
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
                if (!FindDomainSquatsToolOptions.Quiet)
                {
                    foreach ((string, KeyValuePair<string, List<Mutation>>) fail in failedSquats)
                    {
                        Logger.Info($"Failed: {fail.Item1} (rules: {string.Join(',', fail.Item2.Value)})");
                    }
                }

            }
            

            using StreamWriter fw = new(FindDomainSquatsToolOptions.OutputFile);
            string outString = outputBuilder.GetOutput();
            fw.WriteLine(outString);
            fw.Close();
            return ErrorCode.Ok;
        }
    }
}
