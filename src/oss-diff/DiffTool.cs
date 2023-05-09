// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CST.OpenSource.Shared;
using Microsoft.CST.RecursiveExtractor;
using Pastel;
using SarifResult = Microsoft.CodeAnalysis.Sarif.Result;

namespace Microsoft.CST.OpenSource.DiffTool
{
    using Contracts;
    using Microsoft.CST.OpenSource.Helpers;
    using Microsoft.CST.OpenSource.PackageManagers;
    using PackageUrl;

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

            [Option('w', "crawl-archives", Required = false, Default = true,
                HelpText = "Crawl into archives found in packages.")]
            public bool CrawlArchives { get; set; }

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

            [Option('o', "output-location", Required = false, Default = null,
                HelpText = "Output location. Don't specify for console output.")]
            public string? OutputLocation { get; set; } = null;

            [Value(0, Required = true,
                HelpText = "Exactly two Filenames or PackgeURL specifiers to analyze.", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string> Targets { get; set; } = Array.Empty<string>();
        }

        public DiffTool(ProjectManagerFactory projectManagerFactory) : base(projectManagerFactory)
        {
        }

        public DiffTool() : this (new ProjectManagerFactory()) { }

        static async Task Main(string[] args)
        {
            ShowToolBanner();
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.CancelKeyPress += delegate {
                Console.ForegroundColor = originalColor;
            };

            DiffTool? diffTool = new DiffTool();
            await diffTool.ParseOptions<Options>(args).WithParsedAsync<Options>(diffTool.RunAsync);
        }

        public async Task<IOutputBuilder> DiffProjects(Options options)
        {
            Extractor? extractor = new Extractor();
            IOutputBuilder? outputBuilder = OutputBuilderFactory.CreateOutputBuilder(options.Format);
            if (outputBuilder is null)
            {
                Logger.Error($"Format {options.Format} is not supported.");
                throw new ArgumentOutOfRangeException("options.Format", $"Format {options.Format} is not supported.");
            }

            // Map relative location in package to actual location on disk
            ConcurrentDictionary<string, (string, string)> files = new ConcurrentDictionary<string, (string, string)>();
            IEnumerable<string> locations = Array.Empty<string>();
            IEnumerable<string> locations2 = Array.Empty<string>();

            try
            {
                PackageURL purl1 = new PackageURL(options.Targets.First());
                IBaseProjectManager? manager = ProjectManagerFactory.CreateProjectManager(purl1, options.DownloadDirectory ?? Path.GetTempPath());

                if (manager is not null)
                {
                    locations = await manager.DownloadVersionAsync(purl1, true, options.UseCache);
                }
            }
            catch (Exception)
            {
                string? tmpDir = Path.GetTempFileName();
                File.Delete(tmpDir);
                try
                {
                    extractor.ExtractToDirectory(tmpDir, options.Targets.First());
                    locations = new string[] { tmpDir };
                }
                catch (Exception e)
                {
                    Logger.Error($"{e.Message}:{e.StackTrace}");
                    Environment.Exit(-1);
                }
            }

            foreach (string? directory in locations)
            {
                foreach (string? file in System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    files[string.Join(Path.DirectorySeparatorChar, file[directory.Length..].Split(Path.DirectorySeparatorChar)[2..])] = (file, string.Empty);
                }
            }

            try
            {
                PackageURL purl2 = new PackageURL(options.Targets.Last());
                IBaseProjectManager? manager2 = ProjectManagerFactory.CreateProjectManager(purl2, options.DownloadDirectory ?? Path.GetTempPath());

                if (manager2 is not null)
                {
                    locations2 = await manager2.DownloadVersionAsync(purl2, true, options.UseCache);
                }
            }
            catch (Exception)
            {
                string? tmpDir = Path.GetTempFileName();
                File.Delete(tmpDir);
                try
                {
                    extractor.ExtractToDirectory(tmpDir, options.Targets.Last());
                    locations2 = new string[] { tmpDir };
                }
                catch (Exception e)
                {
                    Logger.Error($"{e.Message}:{e.StackTrace}");
                    Environment.Exit(-1);
                }
            }
            foreach (string? directory in locations2)
            {
                foreach (string? file in System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    string? key = string.Join(Path.DirectorySeparatorChar, file[directory.Length..].Split(Path.DirectorySeparatorChar)[2..]);

                    if (files.ContainsKey(key))
                    {
                        (string, string) existing = files[key];
                        existing.Item2 = file;
                        files[key] = existing;
                    }
                    else
                    {
                        files[key] = (string.Empty, file);
                    }
                }
            }

            Parallel.ForEach(files, filePair =>
            {
                ConcurrentBag<Diff>? diffObjs = new ConcurrentBag<Diff>();

                if (options.CrawlArchives)
                {
                    using Stream fs1 = !string.IsNullOrEmpty(filePair.Value.Item1) ? File.OpenRead(filePair.Value.Item1) : new MemoryStream();
                    using Stream fs2 = !string.IsNullOrEmpty(filePair.Value.Item2) ? File.OpenRead(filePair.Value.Item2) : new MemoryStream();
                    IEnumerable<FileEntry>? entries1 = extractor.Extract(new FileEntry(filePair.Key, fs1), new ExtractorOptions() { Parallel = false, MemoryStreamCutoff = 1 });
                    IEnumerable<FileEntry>? entries2 = extractor.Extract(new FileEntry(filePair.Key, fs2), new ExtractorOptions() { Parallel = false, MemoryStreamCutoff = 1 });
                    ConcurrentDictionary<string, (FileEntry?, FileEntry?)>? entryPairs = new ConcurrentDictionary<string, (FileEntry?, FileEntry?)>();
                    foreach (FileEntry? entry in entries1)
                    {
                        entryPairs[entry.FullPath] = (entry, null);
                    }
                    foreach (FileEntry? entry in entries2)
                    {
                        if (entryPairs.ContainsKey(entry.FullPath))
                        {
                            entryPairs[entry.FullPath] = (entryPairs[entry.FullPath].Item1, entry);
                        }
                        else
                        {
                            entryPairs[entry.FullPath] = (null, entry);
                        }
                    }

                    foreach (KeyValuePair<string, (FileEntry?, FileEntry?)> entryPair in entryPairs)
                    {
                        string? text1 = string.Empty;
                        string? text2 = string.Empty;
                        if (entryPair.Value.Item1 is not null)
                        {
                            using StreamReader? sr = new StreamReader(entryPair.Value.Item1.Content);
                            text1 = sr.ReadToEnd();
                        }
                        if (entryPair.Value.Item2 is not null)
                        {
                            using StreamReader? sr = new StreamReader(entryPair.Value.Item2.Content);
                            text2 = sr.ReadToEnd();
                        }
                        WriteFileIssues(entryPair.Key, text1, text2);
                    }
                }
                else
                {
                    string? file1 = string.Empty;
                    string? file2 = string.Empty;
                    if (!string.IsNullOrEmpty(filePair.Value.Item1))
                    {
                        file1 = File.ReadAllText(filePair.Value.Item1);
                    }
                    if (!string.IsNullOrEmpty(filePair.Value.Item2))
                    {
                        file2 = File.ReadAllText(filePair.Value.Item2);
                    }
                    WriteFileIssues(filePair.Key, file1, file2);
                }

                // If we are writing text write the file name
                if (options.Format == "text")
                {
                    if (options.Context > 0 || options.After > 0 || options.Before > 0)
                    {
                        outputBuilder.AppendOutput(new string[] { $"*** {filePair.Key}", $"--- {filePair.Key}", "***************" });
                    }
                    else
                    {
                        outputBuilder.AppendOutput(new string[] { filePair.Key });
                    }
                }
                List<Diff>? diffObjList = diffObjs.ToList();

                // Arrange the diffs in line order
                diffObjList.Sort((x, y) => x.startLine1.CompareTo(y.startLine1));

                foreach (Diff? diff in diffObjList)
                {
                    StringBuilder? sb = new StringBuilder();
                    // Write Context Format
                    if (options.Context > 0 || options.After > 0 || options.Before > 0)
                    {
                        sb.AppendLine($"*** {diff.startLine1 - diff.beforeContext.Count},{diff.endLine1 + diff.afterContext.Count} ****");

                        diff.beforeContext.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"  {x}" : $"  {x}".Pastel(Color.Gray)));
                        diff.text1.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"- {x}" : $"- {x}".Pastel(Color.Red)));
                        diff.afterContext.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"  {x}" : $"  {x}".Pastel(Color.Gray)));

                        if (diff.startLine2 > -1)
                        {
                            sb.AppendLine($"--- {diff.startLine2 - diff.beforeContext.Count},{diff.endLine2 + diff.afterContext.Count} ----");

                            diff.beforeContext.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"  {x}" : $"  {x}".Pastel(Color.Gray)));
                            diff.text2.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"+ {x}" : $"+ {x}".Pastel(Color.Green)));
                            diff.afterContext.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"  {x}" : $"  {x}".Pastel(Color.Gray)));
                        }
                    }
                    // Write diff "Normal Format"
                    else
                    {
                        if (diff.text1.Any() && diff.text2.Any())
                        {
                            sb.Append(Math.Max(diff.startLine1, 0));
                            if (diff.endLine1 != diff.startLine1)
                            {
                                sb.Append($",{diff.endLine1}");
                            }
                            sb.Append('c');
                            sb.Append(Math.Max(diff.startLine2, 0));
                            if (diff.endLine2 != diff.startLine2)
                            {
                                sb.Append($",{diff.endLine2}");
                            }
                            sb.Append(Environment.NewLine);
                            diff.text1.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"< {x}" : $"< {x}".Pastel(Color.Red)));
                            sb.AppendLine("---");
                            diff.text2.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"> {x}" : $"> {x}".Pastel(Color.Green)));
                        }
                        else if (diff.text1.Any())
                        {
                            sb.Append(Math.Max(diff.startLine1, 0));
                            if (diff.endLine1 != diff.startLine1)
                            {
                                sb.Append($",{diff.endLine1}");
                            }
                            sb.Append('d');
                            sb.Append(Math.Max(diff.endLine2, 0));
                            sb.Append(Environment.NewLine);
                            diff.text1.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"< {x}" : $"< {x}".Pastel(Color.Red)));
                        }
                        else if (diff.text2.Any())
                        {
                            sb.Append(Math.Max(diff.startLine1, 0));
                            sb.Append('a');
                            sb.Append(Math.Max(diff.startLine2, 0));
                            if (diff.endLine2 != diff.startLine2)
                            {
                                sb.Append($",{diff.endLine2}");
                            }
                            sb.Append(Environment.NewLine);
                            diff.text2.ForEach(x => sb.AppendLine(options.DownloadDirectory is not null ? $"> {x}" : $"> {x}".Pastel(Color.Green)));
                        }
                    }

                    switch (outputBuilder)
                    {
                        case StringOutputBuilder stringOutputBuilder:
                            stringOutputBuilder.AppendOutput(new string[] { sb.ToString().TrimEnd() });
                            break;
                        case SarifOutputBuilder sarifOutputBuilder:
                            SarifResult? sr = new SarifResult
                            {
                                Locations = new Location[] { new Location() { LogicalLocation = new LogicalLocation() { FullyQualifiedName = filePair.Key } } },
                                AnalysisTarget = new ArtifactLocation() { Uri = new Uri(options.Targets.First()) },
                                Message = new Message() { Text = sb.ToString() }
                            };
                            sarifOutputBuilder.AppendOutput(new SarifResult[] { sr });
                            break;
                    }
                }

                void WriteFileIssues(string path, string file1, string file2)
                {
                    DiffPaneModel? diff = InlineDiffBuilder.Diff(file1, file2);
                    List<string> beforeBuffer = new List<string>();

                    int afterCount = 0;
                    int lineNumber1 = 0;
                    int lineNumber2 = 0;
                    Diff? diffObj = new Diff();

                    foreach (DiffPiece? line in diff.Lines)
                    {
                        switch (line.Type)
                        {
                            case ChangeType.Inserted:
                                lineNumber2++;

                                if (diffObj.lastLineType == Diff.LineType.Context || (diffObj.endLine2 != -1 && lineNumber2 - diffObj.endLine2 > 1 && diffObj.lastLineType != Diff.LineType.Added))
                                {
                                    diffObjs.Add(diffObj);
                                    diffObj = new Diff() { startLine1 = lineNumber1 };
                                }

                                if (diffObj.startLine2 == -1)
                                {
                                    diffObj.startLine2 = lineNumber2;
                                }

                                if (beforeBuffer.Any())
                                {
                                    beforeBuffer.ForEach(x => diffObj.AddBeforeContext(x));
                                    beforeBuffer.Clear();
                                }

                                afterCount = options.After;
                                diffObj.AddText2(line.Text);

                                break;
                            case ChangeType.Deleted:
                                lineNumber1++;

                                if (diffObj.lastLineType == Diff.LineType.Context || (diffObj.endLine1 != -1 && lineNumber1 - diffObj.endLine1 > 1 && diffObj.lastLineType != Diff.LineType.Removed))
                                {
                                    diffObjs.Add(diffObj);
                                    diffObj = new Diff() { startLine2 = lineNumber2 };
                                }

                                if (diffObj.startLine1 == -1)
                                {
                                    diffObj.startLine1 = lineNumber1;
                                }

                                if (beforeBuffer.Any())
                                {
                                    beforeBuffer.ForEach(x => diffObj.AddBeforeContext(x));
                                    beforeBuffer.Clear();
                                }

                                afterCount = options.After;
                                diffObj.AddText1(line.Text);

                                break;
                            default:
                                lineNumber1++;
                                lineNumber2++;

                                if (options.Context == -1)
                                {
                                    diffObj.AddAfterContext(line.Text);
                                    beforeBuffer.Add(line.Text);
                                }
                                else if (afterCount-- > 0)
                                {
                                    diffObj.AddAfterContext(line.Text);
                                    if (afterCount == 0)
                                    {
                                        diffObjs.Add(diffObj);
                                        diffObj = new Diff();
                                    }
                                }
                                else if (options.Before > 0)
                                {
                                    beforeBuffer.Add(line.Text);
                                    while (options.Before < beforeBuffer.Count)
                                    {
                                        beforeBuffer.RemoveAt(0);
                                    }
                                }
                                break;
                        }
                    }

                    if (diffObj.startLine1 != -1 || diffObj.startLine2 != -1)
                    {
                        diffObjs.Add(diffObj);
                    }
                }
            });


            if (!options.UseCache)
            {
                foreach (string? directory in locations.Concat(locations2))
                {
                    FileSystemHelper.RetryDeleteDirectory(directory);
                }
            }

            return outputBuilder;
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

            IOutputBuilder? result = await DiffProjects(options);

            if (options.OutputLocation is null)
            {
                result.PrintOutput();
            }
            else
            {
                result.WriteOutput(options.OutputLocation);
            }
        }
    }
}
