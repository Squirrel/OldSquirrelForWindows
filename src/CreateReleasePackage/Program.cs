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
                var releasesText = File.ReadAllText(releaseFile, Encoding.UTF8);
                var releaseEntries = ReleaseEntry.ParseReleaseFile(releasesText);

                var previousFullRelease = ReleaseEntry.GetPreviousRelease(releaseEntries, package, targetDir);

                if (previousFullRelease != null
                    && File.Exists(previousFullRelease.ReleasePackageFile)) {
                    var deltaFile = Path.Combine(targetDir, package.SuggestedReleaseFileName.Replace("full", "delta"));
                    Console.WriteLine("{0}; {1}", previousFullRelease.InputPackageFile, deltaFile);

                    if (File.Exists(deltaFile)) {
                        File.Delete(deltaFile);
                    }

                    var deltaBuilder = new DeltaPackageBuilder();
                    deltaBuilder.CreateDeltaPackage(previousFullRelease, package, deltaFile);
                }
            }

            ReleaseEntry.BuildReleasesFile(targetDir);

            return 0;
        }
    }
}