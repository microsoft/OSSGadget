using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using Pastel;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource
    {
    class DiffTool : OSSGadget
    {

        public class Options
        {
            [Usage()]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Diff the given packages",
                        new Options { Targets = new List<string>() {"[options]", "package-url package-url-2" } })};
                }
            }

            [Option('d', "download-directory", Required = false, Default = null,
                            HelpText = "the directory to download the packages to.")]
            public string? DownloadDirectory { get; set; } = null;

            [Option('c', "use-cache", Required = false, Default = false,
                HelpText = "Do not download the package if it is already present in the destination directory and do not delete the package after processing.")]
            public bool UseCache { get; set; }

            [Option('B', "context-before", Required = false, Default = 0,
                HelpText = "Number of previous lines to give as context.")]
            public int Before { get; set; } = 0;

            [Option('A', "context-after", Required = false, Default = 0,
                HelpText = "Number of subsequent lines to give as context.")]
            public int After { get; set; } = 0;

            [Option('C', "context", Required = false, Default = 0,
                HelpText = "Number of lines to give as context. Overwrites Before and After options. -1 to print all.")]
            public int Context { get; set; } = 0;

            [Option('a', "added-only", Required = false, Default = false,
                HelpText = "Only show added lines (and requested context).")]
            public bool AddedOnly { get; set; } = false;

            [Option('r', "removed-only", Required = false, Default = false,
                HelpText = "Only show removed lines (and requested context).")]
            public bool RemovedOnly { get; set; } = false;

            [Option('f', "format", Required = false, Default = "text",
                HelpText = "Choose output format. (text|sarifv1|sarifv2)")]
            public string Format { get; set; } = "text";

            [Option('O', "output-location", Required = false, Default = null,
                HelpText = "Output location. Don't specify for console output.")]
            public string? OutputLocation { get; set; } = null;

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string> Targets { get; set; } = Array.Empty<string>();

            
        }
        static async Task Main(string[] args)
        {
            var diffTool = new DiffTool();
            await diffTool.ParseOptions<Options>(args).WithParsedAsync<Options>(diffTool.RunAsync);
        }

        public async Task<string> DiffProjects(Options options)
        {
            var outputBuilder = OutputBuilderFactory.CreateOutputBuilder(options.Format);
            (PackageURL purl1, PackageURL purl2) = (new PackageURL(options.Targets.First()), new PackageURL(options.Targets.Last()));
            var manager = ProjectManagerFactory.CreateProjectManager(purl1, options.DownloadDirectory ?? Path.GetTempPath());
            var manager2 = ProjectManagerFactory.CreateProjectManager(purl2, options.DownloadDirectory ?? Path.GetTempPath());

            if (manager is not null && manager2 is not null)
            {
                var locations = await manager.DownloadVersion(purl1, true, options.UseCache);
                var locations2 = await manager2.DownloadVersion(purl2, true, options.UseCache);

                // Map relative location in package to actual location on disk
                Dictionary<string, (string, string)> files = new Dictionary<string, (string, string)>();

                foreach (var directory in locations)
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        files.Add(string.Join(Path.DirectorySeparatorChar, file.Substring(directory.Length).Split(Path.DirectorySeparatorChar)[2..]), (file, string.Empty));
                    }
                }

                foreach (var directory in locations2)
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        var key = string.Join(Path.DirectorySeparatorChar, file.Substring(directory.Length).Split(Path.DirectorySeparatorChar)[2..]);

                        if (files.ContainsKey(key))
                        {
                            var existing = files[key];
                            existing.Item2 = file;
                            files[key] = existing;
                        }
                        else
                        {
                            files[key] = (string.Empty, file);
                        }
                    }
                }

                foreach (var filePair in files)
                {
                    // If we are writing text write the file name
                    if (options.Format == "text")
                    {
                        outputBuilder.AppendOutput(new string[] { filePair.Key });
                    }

                    var file1 = string.Empty;
                    if (!string.IsNullOrEmpty(filePair.Value.Item1))
                    {
                        file1 = File.ReadAllText(filePair.Value.Item1);
                    }

                    var file2 = string.Empty;
                    if (!string.IsNullOrEmpty(filePair.Value.Item2))
                    {
                        file2 = File.ReadAllText(filePair.Value.Item2);
                    }

                    var diff = InlineDiffBuilder.Diff(file1, file2);

                    List<string> beforeBuffer = new List<string>();

                    int afterCount = 0;

                    foreach (var line in diff.Lines)
                    {
                        switch (line.Type)
                        {
                            case ChangeType.Inserted:
                                if (!options.RemovedOnly || options.AddedOnly)
                                {
                                    if (beforeBuffer.Any())
                                    {
                                        foreach (var buffered in beforeBuffer)
                                        {
                                            switch (outputBuilder)
                                            {
                                                case StringOutputBuilder stringOutputBuilder:
                                                    var outString = options.OutputLocation is null ? $"  {buffered.Pastel(Color.Gray)}" : $"  {buffered}";
                                                    outputBuilder.AppendOutput(new string[] { outString });
                                                    break;
                                            }
                                        }
                                        beforeBuffer.Clear();
                                    }
                                    afterCount = options.After;

                                    switch (outputBuilder)
                                    {
                                        case StringOutputBuilder stringOutputBuilder:
                                            var outString = options.OutputLocation is null ? $"+ {line.Text}".Pastel(Color.Green) : $"+ {line.Text}";
                                            outputBuilder.AppendOutput(new string[] { outString });
                                            break;
                                        case SarifOutputBuilder sarifOutputBuilder:
                                            var sr = new SarifResult();
                                            sr.Locations = new Location[] { new Location() { LogicalLocation = new LogicalLocation() { FullyQualifiedName = filePair.Key } } };
                                            sr.AnalysisTarget = new ArtifactLocation() { Uri = new Uri(purl2.ToString()) };
                                            sr.Message = new Message() { Text = line.Text };
                                            sr.Tags.Add("added");
                                            sarifOutputBuilder.AppendOutput(new SarifResult[] { sr });
                                            break;
                                    }
                                }
                                break;
                            case ChangeType.Deleted:
                                if (!options.AddedOnly || options.RemovedOnly)
                                {
                                    if (beforeBuffer.Any())
                                    {
                                        foreach (var buffered in beforeBuffer)
                                        {
                                            switch (outputBuilder)
                                            {
                                                case StringOutputBuilder stringOutputBuilder:
                                                    var outString = options.OutputLocation is null ? $"  {buffered.Pastel(Color.Gray)}" : $" {buffered}";
                                                    outputBuilder.AppendOutput(new string[] { outString });
                                                    break;
                                            }
                                        }
                                        beforeBuffer.Clear();
                                    }
                                    afterCount = options.After;
                                    switch (outputBuilder)
                                    {
                                        case StringOutputBuilder stringOutputBuilder:
                                            outputBuilder.AppendOutput(new string[] { $"- {line.Text}".Pastel(Color.Red) });
                                            break;
                                        case SarifOutputBuilder sarifOutputBuilder:
                                            var sr = new SarifResult();
                                            sr.Locations = new Location[] { new Location() { LogicalLocation = new LogicalLocation() { FullyQualifiedName = filePair.Key } } };
                                            sr.AnalysisTarget = new ArtifactLocation() { Uri = new Uri(purl1.ToString()) };
                                            sr.Message = new Message() { Text = line.Text };
                                            sr.Tags.Add("removed");
                                            sarifOutputBuilder.AppendOutput(new SarifResult[] { sr });
                                            break;
                                    }
                                }
                                break;
                            default:
                                if (outputBuilder is StringOutputBuilder stringOutputBuilder2)
                                {
                                    if (options.Context == -1)
                                    {
                                        var outString = options.OutputLocation is null ? $"  {line.Text.Pastel(Color.Gray)}" : $"  {line.Text}";
                                        stringOutputBuilder2.AppendOutput(new string[] { outString });
                                    }
                                    else if (afterCount-- > 0)
                                    {
                                        var outString = options.OutputLocation is null ? $"  {line.Text.Pastel(Color.Gray)}" : $"  {line.Text}";
                                        stringOutputBuilder2.AppendOutput(new string[] { outString });
                                    }
                                    else if (options.Before > 0)
                                    {
                                        beforeBuffer.Add(line.Text);
                                        while (options.Before < beforeBuffer.Count)
                                        {
                                            beforeBuffer.RemoveAt(0);
                                        }
                                    }
                                }

                                break;
                        }
                    }
                }

                if (!options.UseCache)
                {
                    foreach (var directory in locations)
                    {
                        Directory.Delete(directory, true);
                    }
                    foreach (var directory in locations2)
                    {
                        Directory.Delete(directory, true);
                    }
                }
            }
            return outputBuilder.GetOutput();
        }

        public async Task RunAsync(Options options)
        {
            if (options.Targets.Count() != 2)
            {
                Logger.Error("Must provide exactly two packages to diff.");
                return;
            }

            if (options.Context > 0)
            {
                options.Before = options.Context;
                options.After = options.Context;
            }
            
            var result = await DiffProjects(options);

            if (options.OutputLocation is null)
            {
                Console.Write(result);
            }
            else
            {
                File.WriteAllText(options.OutputLocation, result);
            }
        }
    }
}
