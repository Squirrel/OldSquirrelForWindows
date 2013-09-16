using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Moq;
using Shimmer.Client;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using Xunit;

namespace Shimmer.Tests.Client
{
    public class UpdateManagerTests
    {
        public class UpdateLocalReleasesTests
        {
            [Fact]
            public void UpdateLocalReleasesSmokeTest()
            {
                string tempDir;
                using (Utility.WithTempDirectory(out tempDir)) {
                    var packageDir = Directory.CreateDirectory(Path.Combine(tempDir, "theApp", "packages"));

                    new[] {
                        "Shimmer.Core.1.0.0.0-full.nupkg",
                        "Shimmer.Core.1.1.0.0-delta.nupkg",
                        "Shimmer.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x => File.Copy(IntegrationTestHelper.GetPath("fixtures", x), Path.Combine(tempDir, "theApp", "packages", x)));

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager("http://lol", "theApp", FrameworkVersion.Net40, tempDir, null, urlDownloader.Object);

                    using (fixture) {
                        fixture.UpdateLocalReleasesFile().Last();
                    }

                    var releasePath = Path.Combine(packageDir.FullName, "RELEASES");
                    File.Exists(releasePath).ShouldBeTrue();

                    var entries = ReleaseEntry.ParseReleaseFile(File.ReadAllText(releasePath, Encoding.UTF8));
                    entries.Count().ShouldEqual(3);
                }
            }

            [Fact]
            public void WhenBothFilesAreInSyncNoUpdatesAreApplied()
            {
                string tempDir;
                using (Utility.WithTempDirectory(out tempDir))
                {
                    var localPackages = Path.Combine(tempDir, "theApp", "packages");
                    var remotePackages = Path.Combine(tempDir, "releases");
                    Directory.CreateDirectory(localPackages);
                    Directory.CreateDirectory(remotePackages);

                    new[] {
                        "Shimmer.Core.1.0.0.0-full.nupkg",
                        "Shimmer.Core.1.1.0.0-delta.nupkg",
                        "Shimmer.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x =>
                    {
                        var path = IntegrationTestHelper.GetPath("fixtures", x);
                        File.Copy(path, Path.Combine(localPackages, x));
                        File.Copy(path, Path.Combine(remotePackages, x));
                    });

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager(remotePackages, "theApp", FrameworkVersion.Net40, tempDir, null, urlDownloader.Object);

                    UpdateInfo updateInfo;
                    using (fixture)
                    {
                        // sync both release files
                        fixture.UpdateLocalReleasesFile().Last();
                        ReleaseEntry.BuildReleasesFile(remotePackages);

                        // check for an update
                        updateInfo = fixture.CheckForUpdate().Wait();
                    }

                    Assert.NotNull(updateInfo);
                    Assert.Empty(updateInfo.ReleasesToApply);
                }
            }

            [Fact]
            public void WhenRemoteReleasesDoNotHaveDeltasNoUpdatesAreApplied()
            {
                string tempDir;
                using (Utility.WithTempDirectory(out tempDir))
                {
                    var localPackages = Path.Combine(tempDir, "theApp", "packages");
                    var remotePackages = Path.Combine(tempDir, "releases");
                    Directory.CreateDirectory(localPackages);
                    Directory.CreateDirectory(remotePackages);

                    new[] {
                        "Shimmer.Core.1.0.0.0-full.nupkg",
                        "Shimmer.Core.1.1.0.0-delta.nupkg",
                        "Shimmer.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x =>
                    {
                        var path = IntegrationTestHelper.GetPath("fixtures", x);
                        File.Copy(path, Path.Combine(localPackages, x));
                    });

                    new[] {
                        "Shimmer.Core.1.0.0.0-full.nupkg",
                        "Shimmer.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x =>
                    {
                        var path = IntegrationTestHelper.GetPath("fixtures", x);
                        File.Copy(path, Path.Combine(remotePackages, x));
                    });

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager(remotePackages, "theApp", FrameworkVersion.Net40, tempDir, null, urlDownloader.Object);

                    UpdateInfo updateInfo;
                    using (fixture)
                    {
                        // sync both release files
                        fixture.UpdateLocalReleasesFile().Last();
                        ReleaseEntry.BuildReleasesFile(remotePackages);

                        // check for an update
                        updateInfo = fixture.CheckForUpdate().Wait();
                    }

                    Assert.NotNull(updateInfo);
                    Assert.Empty(updateInfo.ReleasesToApply);
                }
            }

            [Fact]
            public void WhenTwoRemoteUpdatesAreAvailableChoosesDeltaVersion()
            {
                string tempDir;
                using (Utility.WithTempDirectory(out tempDir))
                {
                    var localPackages = Path.Combine(tempDir, "theApp", "packages");
                    var remotePackages = Path.Combine(tempDir, "releases");
                    Directory.CreateDirectory(localPackages);
                    Directory.CreateDirectory(remotePackages);

                    new[] {
                        "Shimmer.Core.1.0.0.0-full.nupkg",
                    }.ForEach(x =>
                    {
                        var path = IntegrationTestHelper.GetPath("fixtures", x);
                        File.Copy(path, Path.Combine(localPackages, x));
                    });

                    new[] {
                        "Shimmer.Core.1.0.0.0-full.nupkg",
                        "Shimmer.Core.1.1.0.0-delta.nupkg",
                        "Shimmer.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x =>
                    {
                        var path = IntegrationTestHelper.GetPath("fixtures", x);
                        File.Copy(path, Path.Combine(remotePackages, x));
                    });

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager(remotePackages, "theApp", FrameworkVersion.Net40, tempDir, null, urlDownloader.Object);

                    UpdateInfo updateInfo;
                    using (fixture)
                    {
                        // sync both release files
                        fixture.UpdateLocalReleasesFile().Last();
                        ReleaseEntry.BuildReleasesFile(remotePackages);

                        updateInfo = fixture.CheckForUpdate().Wait();

                        Assert.True(updateInfo.ReleasesToApply.First().IsDelta);

                        updateInfo = fixture.CheckForUpdate(ignoreDeltaUpdates:true).Wait();

                        Assert.False(updateInfo.ReleasesToApply.First().IsDelta);
                    }
                }

            }
        }
    }
}
