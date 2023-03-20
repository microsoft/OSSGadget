// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using CommandLine;
using CommandLine.Text;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SemanticVersioning;
using static Microsoft.CST.OpenSource.Shared.OutputBuilderFactory;

namespace Microsoft.CST.OpenSource
{
    using AngleSharp;
    using PackageManagers;
    using PackageUrl;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class FreshTool : OSSGadget
    {
        public FreshTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public FreshTool() : this(new ProjectManagerFactory())
        {
        }

        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Find the source code repository for the given package", new Options { Targets = new List<string>() {"[options]", "package-url..." } })};
                }
            }

            [Option('f', "format", Required = false, Default = "text",
                            HelpText = "specify the output format(text|sarifv1|sarifv2)")]
            public string Format { get; set; } = "text";

            [Option('o', "output-file", Required = false, Default = "",
                HelpText = "send the command output to a file instead of stdout")]
            public string OutputFile { get; set; } = "";

            [Option('m', "max-age-maintained", Required = false, Default = 30 * 18,
                HelpText = "maximum age of versions for still-maintained projects, 0 to disable")]
            public int MaxAgeMaintained { get; set; }

            [Option('u', "max-age-unmaintained", Required = false, Default = 30 * 48,
                HelpText = "maximum age of versions for unmaintained projects, 0 to disable")]
            public int MaxAgeUnmaintained { get; set; }

            [Option('v', "max-out-of-date-versions", Required = false, Default = 6,
                HelpText = "maximum number of versions out of date, 0 to disable")]
            public int MaxOutOfDateVersions { get; set; }

            [Option('r', "filter", Required = false, Default = null,
                HelpText = "filter versions by regular expression")]
            public string? Filter { get; set; }

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string>? Targets { get; set; }
        }

        static async Task Main(string[] args)
        {
            FreshTool freshTool = new FreshTool();
            await freshTool.ParseOptions<Options>(args).WithParsedAsync(freshTool.RunAsync);
        }

        private async Task RunAsync(Options options)
        {
            // select output destination and format
            SelectOutput(options.OutputFile);
            IOutputBuilder outputBuilder = SelectFormat(options.Format);

            int maintainedThresholdDays = options.MaxAgeMaintained > 0 ? options.MaxAgeMaintained : int.MaxValue;
            int nonMaintainedThresholdDays = options.MaxAgeUnmaintained > 0 ? options.MaxAgeUnmaintained : int.MaxValue;
            int maintainedThresholdVersions = options.MaxOutOfDateVersions;

            string? versionFilter = options.Filter;
            
            int safeDays = 90;
            DateTime NOW = DateTime.Now;
            
            maintainedThresholdDays = Math.Max(maintainedThresholdDays, safeDays);
            nonMaintainedThresholdDays = Math.Max(nonMaintainedThresholdDays, safeDays);

            if (options.Targets is IList<string> targetList && targetList.Count > 0)
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        PackageURL? purl = new PackageURL(target);
                        BaseMetadataSource metadataSource = new LibrariesIoMetadataSource();
                        Logger.Info("Collecting metadata for {0}", purl);
                        JsonDocument? metadata = await metadataSource.GetMetadataForPackageUrlAsync(purl, true);
                        if (metadata != null)
                        {
                            JsonElement root = metadata.RootElement;

                            string? latestRelease = root.GetProperty("latest_release_number").GetString();
                            DateTime latestReleasePublishedAt = root.GetProperty("latest_release_published_at").GetDateTime();
                            bool stillMaintained = (NOW - latestReleasePublishedAt).TotalDays < maintainedThresholdDays;

                            // Extract versions
                            IEnumerable<JsonElement> versions = root.GetProperty("versions").EnumerateArray();

                            // Filter if needed
                            if (versionFilter != null)
                            {
                                Regex versionFilterRegex = new Regex(versionFilter, RegexOptions.Compiled);
                                versions = versions.Where(elt => {
                                    string? _version = elt.GetProperty("number").GetString();
                                    if (_version != null)
                                    {
                                        return versionFilterRegex.IsMatch(_version);
                                    }
                                    return true;
                                });
                            }
                            // Order by semantic version
                            versions = versions.OrderBy(elt => {
                                try
                                {
                                    string? _v = elt.GetProperty("number").GetString();
                                    if (_v == null)
                                    {
                                        _v = "0.0.0";
                                    } else if (_v.Count(ch => ch == '.') == 1)
                                    {
                                        _v = _v + ".0";
                                    }
                                    return new SemanticVersioning.Version(_v, true);
                                }
                                catch(Exception)
                                {
                                    return new SemanticVersioning.Version("0.0.0");
                                }
                            });

                            int versionIndex = 0;
                            foreach (JsonElement version in versions)
                            {
                                ++versionIndex;
                                string? versionName = version.GetProperty("number").GetString();
                                DateTime publishedAt = version.GetProperty("published_at").GetDateTime();
                                string? resultMessage = null;
                                
                                if (stillMaintained)
                                {
                                    if ((NOW - publishedAt).TotalDays > maintainedThresholdDays)
                                    {
                                        resultMessage = $"This version {versionName} was published more than {maintainedThresholdDays} days ago.";
                                    }

                                    if (maintainedThresholdVersions > 0 &&
                                        versionIndex < (versions.Count() - maintainedThresholdVersions))
                                    {
                                        if ((NOW - publishedAt).TotalDays > safeDays)
                                        {
                                            if (resultMessage != null )
                                            {
                                                resultMessage += $" In addition, this version was more than {maintainedThresholdVersions} versions out of date.";
                                            }
                                            else
                                            {
                                                resultMessage = $"This version {versionName} was more than {maintainedThresholdVersions} versions out of date.";
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if ((NOW - publishedAt).TotalDays > nonMaintainedThresholdDays)
                                    {
                                        resultMessage = $"This version {versionName} was published more than {nonMaintainedThresholdDays} days ago.";
                                    }
                                }
                                
                                // Write output
                                if (resultMessage != null)
                                {
                                    Console.WriteLine(resultMessage);
                                }
                                else
                                {
                                    Console.WriteLine($"This version {versionName} is current.");
                                }
                                
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error processing {0}: {1}", target, ex.Message);
                    }
                }
            }
        }
    }
}