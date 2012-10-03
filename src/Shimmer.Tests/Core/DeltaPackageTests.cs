using System;
using System.IO;
using System.Linq;
using NuGet;
using ReactiveUI;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using Xunit;

namespace Shimmer.Tests.Core
{
    public class ApplyDeltaPackageTests : IEnableLogger
    {
        [Fact]
        public void ApplyDeltaPackageSmokeTest()
        {
            var basePackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.0.0.0-full.nupkg"));
            var deltaPackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.1.0.0-delta.nupkg"));
            var expectedPackageFile = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.1.0.0-full.nupkg");
            var outFile = Path.GetTempFileName() + ".nupkg";

            try {
                var deltaBuilder = new DeltaPackageBuilder();
                deltaBuilder.ApplyDeltaPackage(basePackage, deltaPackage, outFile);

                var result = new ZipPackage(outFile);
                var expected = new ZipPackage(expectedPackageFile);

                result.Id.ShouldEqual(expected.Id);
                result.Version.ShouldEqual(expected.Version);

                this.Log().Info("Expected file list:");
                expected.GetFiles().Select(x => x.Path).OrderBy(x => x).ForEach(x => this.Log().Info(x));

                this.Log().Info("Actual file list:");
                result.GetFiles().Select(x => x.Path).OrderBy(x => x).ForEach(x => this.Log().Info(x));

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
            var basePackage = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.0.0.0.nupkg");
            var newPackage = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.1.0.0.nupkg");

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

                var deltaBuilder = new DeltaPackageBuilder();
                deltaBuilder.CreateDeltaPackage(baseFixture, fixture, tempFiles[2]);

                var fullPkg = new ZipPackage(tempFiles[1]);
                var deltaPkg = new ZipPackage(tempFiles[2]);

                fullPkg.Id.ShouldEqual(deltaPkg.Id);
                fullPkg.Version.CompareTo(deltaPkg.Version).ShouldEqual(0);

                deltaPkg.GetFiles().Count().ShouldBeGreaterThan(0);

                this.Log().Info("Files in delta package:");
                deltaPkg.GetFiles().ForEach(x => this.Log().Info(x.Path));

                // v1.1 adds a dependency on DotNetZip
                deltaPkg.GetFiles()
                    .Any(x => x.Path.ToLowerInvariant().Contains("ionic.zip"))
                    .ShouldBeTrue();

                // All the other files should be diffs and shasums
                deltaPkg.GetFiles().Any(x => !x.Path.ToLowerInvariant().Contains("ionic.zip")).ShouldBeTrue();
                deltaPkg.GetFiles()
                    .Where(x => !x.Path.ToLowerInvariant().Contains("ionic.zip"))
                    .All(x => x.Path.ToLowerInvariant().EndsWith("diff") || x.Path.ToLowerInvariant().EndsWith("shasum"))
                    .ShouldBeTrue();

                // Every .diff file should have a shasum file
                deltaPkg.GetFiles().Any(x => x.Path.ToLowerInvariant().EndsWith(".diff")).ShouldBeTrue();
                deltaPkg.GetFiles()
                    .Where(x => x.Path.ToLowerInvariant().EndsWith(".diff"))
                    .ForEach(x => {
                                      var lookingFor = x.Path.Replace(".diff", ".shasum");
                                      this.Log().Info("Looking for corresponding shasum file: {0}", lookingFor);
                                      deltaPkg.GetFiles().Any(y => y.Path == lookingFor).ShouldBeTrue();
                    });

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