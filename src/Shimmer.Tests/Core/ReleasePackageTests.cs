using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using MarkdownSharp;
using ReactiveUI;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using NuGet;
using Xunit;

namespace Shimmer.Tests.Core
{
    public class CreateReleasePackageTests : IEnableLogger
    {
        [Fact]
        public void ReleasePackageIntegrationTest()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.0.0.0.nupkg");
            var outputPackage = Path.GetTempFileName() + ".nupkg";
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");

            var fixture = new ReleasePackage(inputPackage);
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            try {
                fixture.CreateReleasePackage(outputPackage, sourceDir);

                this.Log().Info("Resulting package is at {0}", outputPackage);
                var pkg = new ZipPackage(outputPackage);

                int refs = pkg.AssemblyReferences.Count();
                this.Log().Info("Found {0} refs", refs);
                refs.ShouldEqual(0);

                this.Log().Info("Files in release package:");
                pkg.GetFiles().ForEach(x => this.Log().Info(x.Path));

                pkg.GetFiles().Any(x => x.Path.ToLowerInvariant().Contains(@"lib\sl")).ShouldBeFalse();
                pkg.GetFiles().Any(x => x.Path.ToLowerInvariant().Contains(@".xml")).ShouldBeFalse();
            } finally {
                File.Delete(outputPackage);
            }
        }

        [Fact]
        public void FindPackageInOurLocalPackageList()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.0.0.0.nupkg");
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var fixture = ExposedObject.From(new ReleasePackage(inputPackage));
            IPackage result = fixture.findPackageFromName("xunit", VersionUtility.ParseVersionSpec("[1.0,2.0]"), sourceDir, null);

            result.Id.ShouldEqual("xunit");
            result.Version.Version.Major.ShouldEqual(1);
            result.Version.Version.Minor.ShouldEqual(9);
        }

        [Fact]
        public void FindDependentPackagesForDummyPackage()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.0.0.0.nupkg");
            var fixture = ExposedObject.From(new ReleasePackage(inputPackage));
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            IEnumerable<IPackage> results = fixture.findAllDependentPackages(null, sourceDir);
            results.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void CanLoadPackageWhichHasNoDependencies()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.NoDependencies.1.0.0.0.nupkg");
            var outputPackage = Path.GetTempFileName() + ".nupkg";
            var fixture = new ReleasePackage(inputPackage);
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");
            try {
                fixture.CreateReleasePackage(outputPackage, sourceDir);
            }
            finally {
                File.Delete(outputPackage);
            }
        }

        [Fact]
        public void CanResolveMultipleLevelsOfDependencies()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "SampleUpdatingApp.1.0.0.0.nupkg");
            var outputPackage = Path.GetTempFileName() + ".nupkg";
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");

            var fixture = new ReleasePackage(inputPackage);
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            try {
                fixture.CreateReleasePackage(outputPackage, sourceDir);

                this.Log().Info("Resulting package is at {0}", outputPackage);
                var pkg = new ZipPackage(outputPackage);

                int refs = pkg.AssemblyReferences.Count();
                this.Log().Info("Found {0} refs", refs);
                refs.ShouldEqual(0);

                this.Log().Info("Files in release package:");
                pkg.GetFiles().ForEach(x => this.Log().Info(x.Path));

                var filesToLookFor = new[] {
                    "System.Reactive.Core.dll",
                    "ReactiveUI.dll",
                    "Ionic.Zip.dll",
                    "SampleUpdatingApp.exe",
                };

                filesToLookFor.ForEach(name => {
                    this.Log().Info("Looking for {0}", name);
                    pkg.GetFiles().Any(y => y.Path.ToLowerInvariant().Contains(name.ToLowerInvariant())).ShouldBeTrue();
                });
            } finally {
                File.Delete(outputPackage);
            }
        }

        [Fact]
        public void SpecFileMarkdownRenderingTest()
        {
            var dontcare = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.1.0.0.nupkg");
            var inputSpec = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.1.0.0.nuspec");
            var fixture = new ReleasePackage(dontcare);

            var targetFile = Path.GetTempFileName();
            File.Copy(inputSpec, targetFile, true);
                
            try {
                var processor = new Func<string, string>(input =>
                    (new Markdown()).Transform(input));

                // NB: For No Reason At All, renderReleaseNotesMarkdown is 
                // invulnerable to ExposedObject. Whyyyyyyyyy
                var renderMinfo = fixture.GetType().GetMethod("renderReleaseNotesMarkdown", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                renderMinfo.Invoke(fixture, new object[] {targetFile, processor});

                var doc = XDocument.Load(targetFile);
                XNamespace ns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";
                var relNotesElement = doc.Descendants(ns + "releaseNotes").First();
                var htmlText = relNotesElement.Value;

                this.Log().Info("HTML Text:\n{0}", htmlText);

                htmlText.Contains("## Release Notes").ShouldBeFalse();
            } finally {
                File.Delete(targetFile);
            }
        }
    }
}