using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MarkdownSharp;
using Shimmer.Core;

namespace CreateReleasePackage
{
    public class Program
    {
        static int Main(string[] args)
        {
#if DEBUG
            Debugger.Launch();
#endif

            var optParams = ParseCommands.ParseOptions(args);
            if (optParams == null) {
                return -1;
            }

            var targetDir = optParams["target"];
            var package = new ReleasePackage(optParams["input"]);
            var targetFile = Path.Combine(targetDir, package.SuggestedReleaseFileName);

            var fullRelease = package.CreateReleasePackage(targetFile, 
                optParams["pkgdir"] != "" ? optParams["pkgdir"] : null,
                input => (new Markdown()).Transform(input));

            Console.WriteLine("{0};", fullRelease);

            var releaseFile = Path.Combine(targetDir, "RELEASES");
            if (File.Exists(releaseFile)) {
                var releaseEntries = ReleaseEntry.ParseReleaseFile(File.ReadAllText(releaseFile, Encoding.UTF8));

                var latestFullRelease = releaseEntries
                    .Where(x => x.IsDelta == false)
                    .MaxBy(x => x.Version)
                    .Select(x => new ReleasePackage(Path.Combine(targetDir, x.Filename), true))
                    .FirstOrDefault();

                var deltaFile = Path.Combine(targetDir, package.SuggestedReleaseFileName.Replace("full", "delta"));
                Console.WriteLine("{0}; {1}", latestFullRelease.InputPackageFile, deltaFile);

                var deltaBuilder = new DeltaPackageBuilder();
                deltaBuilder.CreateDeltaPackage(package, latestFullRelease, deltaFile);
            }
                
            ReleaseEntry.BuildReleasesFile(targetDir);


            return 0;
        }
    }
}