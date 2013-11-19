using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Options;
using Squirrel.Client;
using Newtonsoft.Json;
using System.Reflection;
using Squirrel.Core;

namespace Squirrel.Updater
{
    class Program
    {
        static int Main(string[] args)
        {
            var command = "";
            var target = "";
            var appName = default(string);
            var showHelp = false;

            var opts = new OptionSet() {
                { "c|command=", "One of 'check' or 'update' to return the latest update information, or to perform the update", v => command = v },
                { "t|update-target=", "The URL or directory to download updates from", v => target = v },
                { "n|app-name=", "(Optional) The name of the application, only necessary if updater.exe isn't in the install directory", v => appName = v},
                { "h|help", "Show this message and exit", v => showHelp = v != null },
            };

            opts.Parse(args);

            if (!new[] { "check", "update", "install", }.Any(x => command.ToLowerInvariant() == x)) {
                Console.Error.WriteLine("Command must be either 'check' or 'update'");
                showHelp = true;
            }

            if (!Directory.Exists(target) && !File.Exists(target) && !isAUrl(target)) {
                Console.Error.WriteLine("Target must be either a directory or a URL to check for updates");
                showHelp = true;
            }

            if (showHelp) {
                Console.WriteLine("\nSquirrel.Updater.exe - Check for updates or update an application");
                Console.WriteLine(@"Usage: Squirrel.Updater.exe [options]");

                Console.WriteLine("Options:");
                foreach(var v in opts) {
                    if (v.GetNames().Length != 2) {
                        Console.WriteLine("  --{0} - {1}", v.GetNames()[0], v.Description);
                    } else {
                        Console.WriteLine("  -{0}/--{1} - {2}", v.GetNames()[0], v.GetNames()[1], v.Description);
                    }
                }

                return 0;
            }

            appName = appName ?? determineAppName();
            using (var mgr = new UpdateManager(target, appName, FrameworkVersion.Net40)) {
                if (command.ToLowerInvariant() == "check") {
                    var updateInfo = default(UpdateInfo);
                    try {
                        updateInfo = mgr.CheckForUpdate().First();
                    } catch (Exception ex) {
                        writeJsonForException(ex, "Failed to check for updates");
                        return -1;
                    }

                    Console.WriteLine(JsonConvert.SerializeObject(new {
                        updateInfo = updateInfo,
                        releaseNotes = updateInfo.FetchReleaseNotes(),
                    }));

                    return 0;
                }

                if (command.ToLowerInvariant() == "update") {
                    var result = default(ReleaseEntry);
                    try {
                        result = mgr.UpdateAppAsync().Result;
                    } catch (Exception ex) {
                        writeJsonForException(ex, "Failed to update application");
                        return -1;
                    }

                    Console.WriteLine(JsonConvert.SerializeObject(result));
                    return 0;
                }

                if (command.ToLowerInvariant() == "install") {
                    var targetRelease = ReleaseEntry.GenerateFromFile(target);
                    mgr.ApplyReleases(UpdateInfo.Create(null, new[] { targetRelease }, Path.GetDirectoryName(target), FrameworkVersion.Net40)).First();
                    return 0;
                }
            }

            throw new Exception("How even did we get here?");
        }

        static string determineAppName()
        {
            var ourDir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            return ourDir.Parent.Name;
        }

        static bool isAUrl(string possiblyAUrl)
        {
            try {
                var foo = new Uri(possiblyAUrl);
                return true;
            } catch {
                return false;
            }
        }

        static void writeJsonForException(Exception ex, string message)
        {
            Console.WriteLine(JsonConvert.SerializeObject(new {
                message = message,
                exceptionInfo = ex.ToString(),
            }));
        }
    }
}
