// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;
using System.Runtime.InteropServices;

namespace Microsoft.CST.OpenSource.Characteristic
{
    public class ApplicationInspectorHarness
    {
        /// <summary>
        /// Special URLs to the current version of Application Inspector
        /// </summary>
        private static readonly Dictionary<OSPlatform, string> DOWNLOAD_URLS = new Dictionary<OSPlatform, string>() {
            { OSPlatform.Windows, "https://github.com/microsoft/ApplicationInspector/releases/download/1.0.27/ApplicationInspector_netcoreapp3.0_1.0.27.zip" },
            { OSPlatform.Linux, "https://github.com/microsoft/ApplicationInspector/releases/download/1.0.27/ApplicationInspector_linux_1.0.27.zip" },
            { OSPlatform.OSX, "https://github.com/microsoft/ApplicationInspector/releases/download/1.0.27/ApplicationInspector_macos_1.0.27.zip" }
        };

        /// <summary>
        /// Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public IList<string> RuleCommands { get; set; } = null;


        public ApplicationInspectorHarness()
        {
        }

        /// <summary>
        /// Checks to ensure that Application Inspector is installed, and if it isn't, attempts
        /// to install it in the current directory.
        /// </summary>
        /// <returns>true iff Application Inspector is now available.</returns>
        public static async Task<bool> EnsureApplicationInspectorAvailable()
        {
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            directoryName = Path.Combine(directoryName, "ApplicationInspector");
            if (!Directory.Exists(directoryName))
            {
                // Get the appropriate download URL (platform-specific)
                string downloadUrl = null;
                foreach (var key in DOWNLOAD_URLS.Keys)
                {
                    if (RuntimeInformation.IsOSPlatform(key))
                    {
                        downloadUrl = DOWNLOAD_URLS[key];
                        break;
                    }
                }

                try
                {
                    Directory.CreateDirectory(directoryName);
                    var webClient = CommonInitialization.WebClient;
                    var response = await webClient.GetByteArrayAsync(downloadUrl);
                    await ExtractArchive(directoryName, response);
                    Logger.Debug("Application Inspector was installed in {0}", directoryName);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error installing Application Inspector: {0}", ex.Message);
                    return false;
                }
            }
            else
            {
                Logger.Warn("Application Inspector already installed.");
                return true;
            }
        }

        /// <summary>
        /// Extracts an archive (given by 'bytes') into a directory named
        /// 'directoryName', recursively, using MultiExtractor.
        /// </summary>
        /// <param name="directoryName"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        protected static async Task ExtractArchive(string directoryName, byte[] bytes)
        {
            Logger.Trace("ExtractArchive({0}, <bytes>)", directoryName);

            Directory.CreateDirectory(directoryName);
            
            var extractor = new Extractor();
            foreach (var fileEntry in extractor.ExtractFile(directoryName, bytes))
            {
                var fullPath = fileEntry.FullPath.Replace(':', Path.DirectorySeparatorChar);
                // @TODO: Does this prevent zip-slip?
                foreach (var c in Path.GetInvalidPathChars())
                {
                    fullPath = fullPath.Replace(c, '-');
                }
                var filePathToWrite = Path.Combine(directoryName, fullPath);
                filePathToWrite = filePathToWrite.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.GetDirectoryName(filePathToWrite));
                await File.WriteAllBytesAsync(filePathToWrite, fileEntry.Content.ToArray());
            }
            Logger.Debug($"Archive extracted to {directoryName}");
        }

        public async Task<JsonDocument> ExecuteApplicationInspector(string targetDirectory)
        {
            Logger.Trace("ExecuteApplicationInspector({0})", targetDirectory);

            if (!await EnsureApplicationInspectorAvailable())
            {
                Logger.Warn("Application Inspector is not available.");
                return default;
            }
            var tempFileName = Path.GetTempFileName();
            var appInspectorDll = Directory.EnumerateFiles("ApplicationInspector", "AppInspector.dll", new EnumerationOptions { RecurseSubdirectories = true }).FirstOrDefault();
            var appInspectorDir = Path.GetDirectoryName(appInspectorDll);
            using var process = new Process
            {
                #pragma warning disable SEC0032 // Command Injection Process Start Info
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = Path.GetFullPath(Path.Combine(appInspectorDir, "AppInspector.exe")),
                    Arguments = "",
                    CreateNoWindow = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    
                }
                #pragma warning restore SEC0032 // Command Injection Process Start Info
            };
            process.StartInfo.ArgumentList.Add("analyze");
            process.StartInfo.ArgumentList.Add("-f");
            process.StartInfo.ArgumentList.Add("json");
            process.StartInfo.ArgumentList.Add("-o");
            process.StartInfo.ArgumentList.Add(tempFileName);
            process.StartInfo.ArgumentList.Add("-s");
            process.StartInfo.ArgumentList.Add(targetDirectory);

            if (RuleCommands != null)
            {
                Logger.Trace("Adding custom rule commands: {0}", string.Join(',', RuleCommands));
                foreach (var ruleCommand in RuleCommands)
                {
                    process.StartInfo.ArgumentList.Add(ruleCommand);
                }
            }

            Logger.Trace("Application Inspector starting...");
            process.Start();
            
            var processStdout = process.StandardOutput.ReadToEnd();
            var processStderr = process.StandardError.ReadToEnd();

            process.WaitForExit();
            
            Logger.Trace("Application Inspector exited, exit code {0}", process.ExitCode);

            if (process.ExitCode != 0 && process.ExitCode != 1)
            {
                Logger.Warn("Error running Application Inspector. Ouptut and error text follows:");
                Logger.Warn(processStdout);
                Logger.Warn(processStderr);
                
                // Clean up
                if (File.Exists(tempFileName))
                {
                    #pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
                    File.Delete(tempFileName);
                    #pragma warning restore SEC0116 // Path Tampering Unvalidated File Path
                }
                return null;
            }
            else
            {
                JsonDocument result = default;
                #pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
                using (var fileStream = File.OpenRead(tempFileName))
                #pragma warning restore SEC0116 // Path Tampering Unvalidated File Path
                {
                    result = await JsonDocument.ParseAsync(fileStream);
                }

                // Clean up
                #pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
                File.Delete(tempFileName);
                #pragma warning restore SEC0116 // Path Tampering Unvalidated File Path
                return result;
            }
        }
    }
}
