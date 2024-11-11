// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.OssGadget.CLI.Tools;

using Microsoft.CST.OpenSource;
using Microsoft.CST.OpenSource.Helpers;
using Microsoft.CST.OpenSource.PackageManagers;
using NLog;
using PackageUrl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.OssGadget.Options;
using Options;

public class DownloadTool : BaseTool<DownloadToolOptions>
{
    private readonly ProjectManagerFactory _projectManagerFactory;
    private readonly ILogger _logger;

    public DownloadTool(ProjectManagerFactory projectManagerFactory)
    {
        _projectManagerFactory = projectManagerFactory;
        _logger = CliHelpers.Logger;
    }
    
    public async Task<ErrorCode> RunAsync(DownloadToolOptions options)
        {
            if (options.Targets is IEnumerable<string> targetList && targetList.Any())
            {
                foreach (string? target in targetList)
                {
                    try
                    {
                        // PackageURL requires the @ in a namespace declaration to be escaped
                        // We find if the namespace contains an @ in the namespace
                        // And replace it with %40
                        string escapedNameSpaceTarget = CliHelpers.EscapeAtSymbolInNameSpace(target);
                        PackageURL? purl = new PackageURL(escapedNameSpaceTarget);
                        string downloadDirectory = options.DownloadDirectory == "." ? System.IO.Directory.GetCurrentDirectory() : options.DownloadDirectory;
                        bool useCache = options.UseCache;
                        PackageDownloader? packageDownloader = new PackageDownloader(purl, _projectManagerFactory, downloadDirectory, useCache);

                        List<string>? downloadResults = await packageDownloader.DownloadPackageLocalCopy(purl, options.DownloadMetadataOnly, options.Extract);
                        foreach (string? downloadPath in downloadResults)
                        {
                            if (string.IsNullOrEmpty(downloadPath))
                            {
                                _logger.Error("Unable to download {0}.", purl.ToString());
                            }
                            else
                            {
                                _logger.Info("Downloaded {0} to {1}", purl.ToString(), downloadPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Error processing {0}: {1}", target, ex.Message);
                        return ErrorCode.ProcessingException;
                    }
                }
            }
            else
            {
                _logger.Error("No targets were specified for downloading.");
                return ErrorCode.NoTargets;
            }
            return ErrorCode.Ok;
        }
}