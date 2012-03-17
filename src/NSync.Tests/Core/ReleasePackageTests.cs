using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                pkg.GetFiles().Any(x => x.Path.ToLowerInvariant().Contains(@"lib\sl")).ShouldBeFalse();
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