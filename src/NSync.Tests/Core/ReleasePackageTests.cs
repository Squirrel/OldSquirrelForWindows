using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NSync.Core;
using NSync.Tests.TestHelpers;
using NuGet;
using Xunit;

namespace NSync.Tests.Core
{
    public class CreateReleasePackageTests : IEnableLogger
    {
        [Fact]
        public void ReleasePackageIntegrationTest()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.0.0.0.nupkg");
            var outputPackage = Path.GetTempFileName() + ".nupkg";
            var fixture = new ReleasePackage(inputPackage);
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            try {
                fixture.CreateReleasePackage(outputPackage, sourceDir);

                this.Log().Info("Resulting package is at {0}", outputPackage);
                var pkg = new ZipPackage(outputPackage);

                int refs = pkg.References.Count();
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
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.0.0.0.nupkg");
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var fixture = ExposedObject.From(new ReleasePackage(inputPackage));
            IPackage result = fixture.findPackageFromName("xunit", VersionUtility.ParseVersionSpec("[1.0,2.0]"), sourceDir, null);

            result.Id.ShouldEqual("xunit");
            result.Version.Major.ShouldEqual(1);
            result.Version.Minor.ShouldEqual(9);
        }

        [Fact]
        public void FindDependentPackagesForDummyPackage()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.0.0.0.nupkg");
            var fixture = ExposedObject.From(new ReleasePackage(inputPackage));
            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            IEnumerable<IPackage> results = fixture.findAllDependentPackages(null, sourceDir);
            results.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void SpecFileMarkdownRenderingTest()
        {
            var dontcare = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.1.0.0.nupkg");
            var inputSpec = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.1.0.0.nuspec");
            var fixture = new ReleasePackage(dontcare);

            var targetFile = Path.GetTempFileName();
            File.Copy(inputSpec, targetFile, true);
                
            try {
                // NB: For No Reason At All, renderReleaseNotesMarkdown is 
                // invulnerable to ExposedObject. Whyyyyyyyyy
                var renderMinfo = fixture.GetType().GetMethod("renderReleaseNotesMarkdown", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                renderMinfo.Invoke(fixture, new object[] {targetFile});

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

        [Fact]
        public void ApplyDeltaPackageTest()
        {
            var basePackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.0.0.0-full.nupkg"));
            var deltaPackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.1.0.0-delta.nupkg"));
            var expectedPackageFile = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.1.0.0-full.nupkg");
            var outFile = Path.GetTempFileName() + ".nupkg";

            try {
                basePackage.ApplyDeltaPackage(deltaPackage, outFile);
                var result = new ZipPackage(outFile);
                var expected = new ZipPackage(expectedPackageFile);

                result.Id.ShouldEqual(expected.Id);
                result.Version.ShouldEqual(expected.Version);

                Enumerable.Zip(
                    expected.GetFiles().Select(x => x.Path).OrderBy(x => x),
                    result.GetFiles().Select(x => x.Path).OrderBy(x => x),
                    (e, a) => e == a 
                ).All(x => x).ShouldBeTrue();
            } finally {
                if (File.Exists(outFile)) {
                    File.Delete(outFile);
                }
            }
        }
    }

    public class CreateDeltaPackageTests : IEnableLogger
    {
        [Fact]
        public void CreateDeltaPackageIntegrationTest()
        {
            var basePackage = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.0.0.0.nupkg");
            var newPackage = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.1.0.0.nupkg");

            var sourceDir = IntegrationTestHelper.GetPath("..", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var baseFixture = new ReleasePackage(basePackage);
            var fixture = new ReleasePackage(newPackage);

            var tempFiles = Enumerable.Range(0, 3)
                .Select(_ => Path.GetTempPath() + Guid.NewGuid().ToString() + ".nupkg")
                .ToArray();

            try {
                baseFixture.CreateReleasePackage(tempFiles[0], sourceDir);
                fixture.CreateReleasePackage(tempFiles[1], sourceDir);

                (new FileInfo(baseFixture.ReleasePackageFile)).Exists.ShouldBeTrue();
                (new FileInfo(fixture.ReleasePackageFile)).Exists.ShouldBeTrue();

                fixture.CreateDeltaPackage(baseFixture, tempFiles[2]);

                var fullPkg = new ZipPackage(tempFiles[1]);
                var deltaPkg = new ZipPackage(tempFiles[2]);

                fullPkg.Id.ShouldEqual(deltaPkg.Id);
                fullPkg.Version.CompareTo(deltaPkg.Version).ShouldEqual(0);

                // v1.1 adds a dependency on DotNetZip
                deltaPkg.GetFiles()
                    .Any(x => x.Path.ToLowerInvariant().Contains("ionic.zip"))
                    .ShouldBeTrue();

                // All the other files should be diffs
                deltaPkg.GetFiles()
                    .Where(x => !x.Path.ToLowerInvariant().Contains("ionic.zip"))
                    .All(x => x.Path.ToLowerInvariant().EndsWith("diff"))
                    .ShouldBeTrue();

                // Delta packages should be smaller than the original!
                var fileInfos = tempFiles.Select(x => new FileInfo(x)).ToArray();
                this.Log().Info("Base Size: {0}, Current Size: {1}, Delta Size: {2}",
                    fileInfos[0].Length, fileInfos[1].Length, fileInfos[2].Length);

                (fileInfos[2].Length - fileInfos[1].Length).ShouldBeLessThan(0);
            } finally {
                tempFiles.ForEach(File.Delete);
            }
        }
    }
}