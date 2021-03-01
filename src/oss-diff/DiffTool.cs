using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CST.OpenSource.Shared;

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
                HelpText = "do not download the package if it is already present in the destination directory.")]
            public bool UseCache { get; set; }

            [Option('d', "delete-after-diff", Required = false, Default = false,
                HelpText = "delete the packages after diffing them.")]
            public bool DeleteAfterDiff { get; set; }

            [Option('B', "context-before", Required = false, Default = 0,
                HelpText = "Number of previous lines to give as context.")]
            public int Before { get; set; } = 0;

            [Option('A', "context-after", Required = false, Default = 0,
                HelpText = "Number of subsequent lines to give as context.")]
            public int After { get; set; } = 0;

            [Option('C', "context", Required = false, Default = 0,
                HelpText = "Number of lines to give as context. Overwrites Before and After options.")]
            public int Context { get; set; } = 0;

            [Value(0, Required = true,
                HelpText = "PackgeURL(s) specifier to analyze (required, repeats OK)", Hidden = true)] // capture all targets to analyze
            public IEnumerable<string> Targets { get; set; } = Array.Empty<string>();

            
        }
        static async Task Main(string[] args)
        {
            var diffTool = new DiffTool();
            await diffTool.ParseOptions<Options>(args).WithParsedAsync<Options>(diffTool.RunAsync);
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

            (PackageURL purl1, PackageURL purl2) = (new PackageURL(options.Targets.First()), new PackageURL(options.Targets.Last()));
            var manager = ProjectManagerFactory.CreateProjectManager(purl1, options.DownloadDirectory ?? Path.GetTempPath());
            var manager2 = ProjectManagerFactory.CreateProjectManager(purl2, options.DownloadDirectory ?? Path.GetTempPath());

            if (manager is not null && manager2 is not null)
            {
                var locations = await manager.DownloadVersion(purl1, true, options.UseCache);

                var locations2 = await manager2.DownloadVersion(purl2, true, options.UseCache);

                Dictionary<string, (string, string)> files = new Dictionary<string, (string, string)>();
                foreach (var directory in locations)
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        var contents = File.ReadAllText(file);
                        files.Add(string.Join(Path.DirectorySeparatorChar,file.Substring(directory.Length).Split(Path.DirectorySeparatorChar)[2..]), (contents, string.Empty));
                    }
                }

                foreach (var directory in locations2)
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        var contents = File.ReadAllText(file);
                        var key = string.Join(Path.DirectorySeparatorChar, file.Substring(directory.Length).Split(Path.DirectorySeparatorChar)[2..]);

                        if (files.ContainsKey(key))
                        {
                            var existing = files[key];
                            existing.Item2 = contents;
                            files[key] = existing;
                        }
                        else
                        {
                            files[key] = (string.Empty, contents);
                        }
                    }
                }

                foreach (var filePair in files)
                {
                    var diff = InlineDiffBuilder.Diff(filePair.Value.Item1, filePair.Value.Item2);
                    Console.WriteLine(filePair.Key);
                    var savedColor = Console.ForegroundColor;
                    List<string> beforeBuffer = new List<string>();
                    int afterCount = 0;
                    foreach (var line in diff.Lines)
                    {
                        switch (line.Type)
                        {
                            case ChangeType.Inserted:
                                if (beforeBuffer.Any())
                                {
                                    foreach(var buffered in beforeBuffer)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Gray; // compromise for dark or light background
                                        Console.Write("  ");
                                        Console.WriteLine(buffered);
                                    }
                                    beforeBuffer.Clear();
                                }
                                afterCount = options.After;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.Write("+ ");
                                Console.WriteLine(line.Text);
                                break;
                            case ChangeType.Deleted:
                                if (beforeBuffer.Any())
                                {
                                    foreach (var buffered in beforeBuffer)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Gray; // compromise for dark or light background
                                        Console.Write("  ");
                                        Console.WriteLine(buffered);
                                    }
                                    beforeBuffer.Clear();
                                }
                                afterCount = options.After;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write("- ");
                                Console.WriteLine(line.Text);
                                break;
                            default:
                                if (afterCount-- > 0)
                                {
                                    Console.ForegroundColor = ConsoleColor.Gray; // compromise for dark or light background
                                    Console.Write("  ");
                                    Console.WriteLine(line.Text);
                                }
                                else
                                {
                                    beforeBuffer.Add(line.Text);
                                    while(beforeBuffer.Count > options.Before)
                                    {
                                        beforeBuffer.RemoveAt(0);
                                    }
                                }

                                break;
                        }
                    }
                    Console.ForegroundColor = savedColor;
                }
            }
        }
    }
}
