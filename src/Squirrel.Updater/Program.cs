using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Squirrel.Updater
{
    class Program
    {
        static int Main(string[] args)
        {
            var command = "";
            var target = "";
            var showHelp = false;

            var opts = new OptionSet() {
                { "c|command=", "One of 'check' or 'update' to return the latest update information, or to perform the update", v => command = v },
                { "t|update-target=", "The URL or directory to download updates from", v => target = v },
                { "h|help", "Show this message and exit", v => showHelp = v != null },
            };

            opts.Parse(args);

            if (!new[] { "check", "update" }.Any(x => command.ToLowerInvariant() == x)) {
                Console.Error.WriteLine("Command must be either 'check' or 'update'");
                showHelp = true;
            }

            if (!Directory.Exists(target) && !isAUrl(target)) {
                Console.Error.WriteLine("Target must be either a directory or a URL to check for updates");
                showHelp = true;
            }

            if (showHelp) {
                Console.WriteLine("\nCreateReleasePackage - take a NuGet package and create a Release Package");
                Console.WriteLine(@"Usage: CreateReleasePackage.exe [Options] \path\to\app.nupkg");

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

            return 0;
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
    }
}
