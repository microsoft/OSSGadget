using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class SquatChecker
    {
        Generative gen { get; set; }

        public class Options
        {
            public int SleepDelay { get; set; }
        }
        public SquatChecker()
        {
            gen = new Generative();
        }
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        public async IAsyncEnumerable<FindSquatResult> CheckSquats(PackageURL purl, Options options)
        {
            if (purl.Name is not null && purl.Type is not null)
            {
                var manager = ProjectManagerFactory.CreateProjectManager(purl, null);
                if (manager is not null)
                {
                    var mutationsDict = gen.Mutate(purl);

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
                        FindSquatResult? res = null;
                        try
                        {
                            var versions = await manager.EnumerateVersions(candidatePurl);

                            if (versions.Any())
                            {
                                res = new FindSquatResult(
                                    packageName: candidate,
                                    packageUrl: candidatePurl,
                                    squattedPackage: purl,
                                    rules: rules);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Trace($"Could not enumerate versions. Package {candidate} likely doesn't exist. {e.Message}:{e.StackTrace}");
                        }
                        if (res is not null)
                        {
                            yield return res;
                        }
                    }
                }
            }
        }
    }
}
