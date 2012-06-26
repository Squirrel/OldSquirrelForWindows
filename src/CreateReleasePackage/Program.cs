using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Options;
using NSync.Core;

namespace CreateReleasePackage
{
    class Program
    {
        static int Main(string[] args)
        {
            var optParams = parseOptions(args);
            if (optParams == null) {
                optParams = new Dictionary<string, string>();
                optParams["target"] = @"C:\Users\Administrator\Documents\GitHub\nsync\src\CreateReleasePackage\Releases";
                optParams["input"] = @"Z:\Dropbox\ReactiveUI_Build_DontFuckingDeleteThisAgain\NuGet\reactiveui-core.3.1.1.nupkg";
                optParams["pkgdir"] = @" Z:\Dropbox\ReactiveUI_External\Packages";
            }

            var targetDir = optParams["target"];
            var package = new ReleasePackage(optParams["input"]);
            var targetFile = Path.Combine(targetDir, package.SuggestedReleaseFileName);

            package.CreateReleasePackage(targetFile, optParams["pkgdir"] != "" ? optParams["pkgdir"] : null);

            var releaseFile = Path.Combine(targetDir, "RELEASES");
            if (File.Exists(releaseFile)) {
                var releaseEntries = ReleaseEntry.ParseReleaseFile(File.ReadAllText(releaseFile, Encoding.UTF8));

                var latestFullRelease = releaseEntries
                    .Where(x => x.IsDelta == false)
                    .MaxBy(x => x.Version)
                    .Select(x => new ReleasePackage(Path.Combine(targetDir, x.Filename), true))
                    .FirstOrDefault();

                var deltaFile = Path.Combine(targetDir, package.SuggestedReleaseFileName.Replace("full", "delta"));
                Console.WriteLine("{0} {1}", latestFullRelease.InputPackageFile, deltaFile);
                package.CreateDeltaPackage(latestFullRelease, deltaFile);
            }
                
            ReleaseEntry.BuildReleasesFile(targetDir);

            return 0;
        }

        static Dictionary<string, string> parseOptions(string[] args)
        {
            bool showHelp = false;
            string targetDir = null;
            string packagesDir = null;

            var opts = new OptionSet() {
                { "p|packages-directory=", "(Optional) The NuGet packages directory to use, omit to use default", v => packagesDir = v },
                { "o|output-directory=", "The target directory to put the generated file", v => targetDir = v },
                { "h|help", "Show this message and exit", v => showHelp = v != null },
            };

            var filename = opts.Parse(args).FirstOrDefault();
            showHelp = (String.IsNullOrEmpty(filename));

            if (!File.Exists(filename)) {
                Console.Error.WriteLine("'{0}' doesn't exist. Please specify an existing NuGet package", filename);
                showHelp = true;
            }

            if (String.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) {
                Console.Error.WriteLine("'{0}' doesn't exist. Please specify an existing Release directory", filename);
                showHelp = true;
            }

            if (!String.IsNullOrEmpty(packagesDir) && !Directory.Exists(packagesDir)) {
                Console.Error.WriteLine("'{0}' doesn't exist. Please specify an existing packages directory", filename);
                showHelp = true;
            }

            if (showHelp) {
                Console.WriteLine("\nCreateReleasePackage - take a NuGet package and create a Release Package");
                Console.WriteLine(@"Usage: CreateReleasePackage.exe [Options] \path\to\app.nupkg");

                Console.WriteLine("Options:");
                foreach(var v in opts) {
                    Console.WriteLine("  -{0}/--{1} - {2}", v.GetNames()[0], v.GetNames()[1], v.Description);
                }

                return null;
            }

            return new Dictionary<string, string>() {
                { "input", filename },
                { "target", targetDir },
                { "pkgdir", packagesDir ?? ""},
            };
        }
    }
}
