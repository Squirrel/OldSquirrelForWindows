using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Options;
using NuGet;
using ReactiveUI;
using Shimmer.Core;

namespace CreateReleasePackage
{
    class Program
    {
        static int Main(string[] args)
        {
            RxApp.LoggerFactory = _ => new NullLogger();

            var optParams = parseOptions(args);
            if (optParams == null) {
                return -1;
            }

            var targetDir = optParams["target"];
            var package = new ReleasePackage(optParams["input"]);
            var targetFile = Path.Combine(targetDir, package.SuggestedReleaseFileName);

            var fullRelease = package.CreateReleasePackage(targetFile, optParams["pkgdir"] != "" ? optParams["pkgdir"] : null);

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

                var deltaBuilder = new DeltaPackageBuilder();
                deltaBuilder.CreateDeltaPackage(package, latestFullRelease, deltaFile);
            }
                
            ReleaseEntry.BuildReleasesFile(targetDir);

            Console.WriteLine(fullRelease);
            return 0;
        }

        static Dictionary<string, string> parseOptions(string[] args)
        {
            bool showHelp = false;
            string targetDir = null;
            string packagesDir = null;
            string templateSource = null;

            var opts = new OptionSet() {
                { "p|packages-directory=", "(Optional) The NuGet packages directory to use, omit to use default", v => packagesDir = v },
                { "o|output-directory=", "The target directory to put the generated file", v => targetDir = v },
                { "preprocess-template=", "The template file to parse. Part of Create-Release.ps1, ignore this", v => templateSource = v },
                { "h|help", "Show this message and exit", v => showHelp = v != null },
            };

            var filename = opts.Parse(args).FirstOrDefault();
            showHelp = (String.IsNullOrEmpty(filename));

            if (!File.Exists(filename)) {
                Console.Error.WriteLine("'{0}' doesn't exist. Please specify an existing NuGet package", filename);
                showHelp = true;
            }

            if (templateSource != null) {
                Console.WriteLine(processTemplateFile(filename, templateSource));
                return null;
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
                    if (v.GetNames().Length != 2) {
                        Console.WriteLine("  --{0} - {1}", v.GetNames()[0], v.Description);
                    } else {
                        Console.WriteLine("  -{0}/--{1} - {2}", v.GetNames()[0], v.GetNames()[1], v.Description);
                    }
                }

                return null;
            }

            return new Dictionary<string, string>() {
                { "input", filename },
                { "target", targetDir },
                { "pkgdir", packagesDir ?? ""},
            };
        }

        static string processTemplateFile(string packageFile, string templateFile)
        {
            var zp = new ZipPackage(packageFile);
            var noBetaRegex = new Regex(@"-.*$");

            var toSub = new[] {
                new {Name = "Authors", Value = String.Join(", ", zp.Authors ?? Enumerable.Empty<string>())},
                new {Name = "Description", Value = zp.Description},
                new {Name = "IconUrl", Value = zp.IconUrl != null ? zp.IconUrl.ToString() : ""},
                new {Name = "LicenseUrl", Value = zp.LicenseUrl != null ? zp.LicenseUrl.ToString() : ""},
                new {Name = "ProjectUrl", Value = zp.ProjectUrl != null ? zp.ProjectUrl.ToString() : ""},
                new {Name = "Summary", Value = zp.Summary ?? zp.Title },
                new {Name = "Title", Value = zp.Title},
                new {Name = "Version", Value = noBetaRegex.Replace(zp.Version.ToString(), "") },
            };

            var output = toSub.Aggregate(new StringBuilder(File.ReadAllText(templateFile)), (acc, x) =>
                { acc.Replace(String.Format("$(var.NuGetPackage_{0})", x.Name), x.Value); return acc; });

            var ret = Path.GetTempFileName();
            File.WriteAllText(ret, output.ToString(), Encoding.UTF8);
            return ret;
        }
    }
}
