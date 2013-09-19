using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using Moq;
using NuGet;
using Shimmer.Client;
using Shimmer.Client.WiXUi;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using Shimmer.WiXUi.ViewModels;
using Xunit;
using ErrorEventArgs = Microsoft.Tools.WindowsInstallerXml.Bootstrapper.ErrorEventArgs;

namespace Shimmer.Tests.Client
{
    public class InstallManagerTests
    {
        [Fact(Skip="The Zip test has some zero values - too fast lol")]
        public void EigenUpdateWithoutUpdateURL()
        {
            string dir;
            string outDir;

            using (Utility.WithTempDirectory(out outDir))
            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir)) {
                var di = new DirectoryInfo(dir);
                var progress = new Subject<int>();

                var bundledRelease = ReleaseEntry.GenerateFromFile(di.GetFiles("*.nupkg").First().FullName);
                var fixture = new InstallManager(bundledRelease, outDir);
                var pkg = new ZipPackage(Path.Combine(dir, "SampleUpdatingApp.1.1.0.0.nupkg"));

                var progressValues = new List<int>();
                progress.Subscribe(progressValues.Add);

                fixture.ExecuteInstall(dir, pkg, progress).Wait();

                var filesToLookFor = new[] {
                    "SampleUpdatingApp\\app-1.1.0.0\\SampleUpdatingApp.exe",
                    "SampleUpdatingApp\\packages\\RELEASES",
                    "SampleUpdatingApp\\packages\\SampleUpdatingApp.1.1.0.0.nupkg",
                };

                filesToLookFor.ForEach(f => Assert.True(File.Exists(Path.Combine(outDir, f)), "Could not find file: " + f));

                // Progress should be monotonically increasing
                progressValues.Count.ShouldBeGreaterThan(2);
                progressValues.Zip(progressValues.Skip(1), (prev, cur) => cur - prev).All(x => x > 0).ShouldBeTrue();
            }
        }

        [Fact(Skip = "TODO")]
        public void EigenUpdateWithUpdateURL()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip="TODO")]
        public void UpdateReportsProgress()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "TODO")]
        public void InstallHandlesAccessDenied()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void InstallRunsHooks()
        {
            string dir;
            string outDir;

            var package = "SampleUpdatingApp.1.2.0.0.nupkg";

            using (Utility.WithTempDirectory(out outDir))
            using (IntegrationTestHelper.WithFakeInstallDirectory(package,out dir)) {
                var di = new DirectoryInfo(dir);

                var bundledRelease = ReleaseEntry.GenerateFromFile(di.GetFiles("*.nupkg").First().FullName);
                var fixture = new InstallManager(bundledRelease, outDir);
                var pkg = new ZipPackage(Path.Combine(dir, package));

                fixture.ExecuteInstall(dir, pkg).Wait();

                var generatedFile = Path.Combine(outDir, "SampleUpdatingApp", "app-1.2.0.0", "install");

                Assert.True(File.Exists(generatedFile));
            }
        }

        [Fact]
        public void UninstallRunsHooks()
        {
            string dir;
            string outDir;

            var package = "SampleUpdatingApp.1.2.0.0.nupkg";

            using (Utility.WithTempDirectory(out outDir))
            using (IntegrationTestHelper.WithFakeInstallDirectory(package, out dir))
            {
                var di = new DirectoryInfo(dir);

                var bundledRelease = ReleaseEntry.GenerateFromFile(di.GetFiles("*.nupkg").First().FullName);
                var fixture = new InstallManager(bundledRelease, outDir);
                var pkg = new ZipPackage(Path.Combine(dir, package));

                fixture.ExecuteInstall(dir, pkg).Wait();
                fixture.ExecuteUninstall(new Version("1.2.0.0")).Wait();

                var generatedFile = Path.Combine(outDir, "uninstall");

                Assert.True(File.Exists(generatedFile));
            }
        }

        [Fact]
        public void UninstallRemovesEverything()
        {
            string dir;
            string appDir;

            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir))
            using (IntegrationTestHelper.WithFakeAlreadyInstalledApp(out appDir)) {
                var di = new DirectoryInfo(dir);
                var progress = new Subject<int>();

                var bundledRelease = ReleaseEntry.GenerateFromFile(di.GetFiles("*.nupkg").First().FullName);
                var fixture = new InstallManager(bundledRelease, appDir);

                var progressValues = new List<int>();
                progress.Subscribe(progressValues.Add);

                fixture.ExecuteUninstall().First();

                di = new DirectoryInfo(appDir);
                di.GetDirectories().Any().ShouldBeFalse();
                di.GetFiles().Any().ShouldBeFalse();
            }
        }

        [Fact]
        public void UninstallDoesntCrashOnMissingAppDirectory()
        {
            string dir;
            string appDir;
            InstallManager fixture;

            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir))
            using (IntegrationTestHelper.WithFakeAlreadyInstalledApp(out appDir)) {
                var di = new DirectoryInfo(dir);

                var bundledRelease = ReleaseEntry.GenerateFromFile(di.GetFiles("*.nupkg").First().FullName);
                fixture = new InstallManager(bundledRelease, appDir);
            }
            
            fixture.ExecuteUninstall().First();
        }

        [Fact]
        public void InstallWithContentInPackageDropsInSameFolder()
        {
            string dir;
            string outDir;

            var package = "ProjectWithContent.1.0.0.0-beta-full.nupkg";

            using (Utility.WithTempDirectory(out outDir))
            using (IntegrationTestHelper.WithFakeInstallDirectory(package, out dir))
            {
                try
                {
                    var di = new DirectoryInfo(dir);

                    var bundledRelease = ReleaseEntry.GenerateFromFile(di.GetFiles("*.nupkg").First().FullName);
                    var fixture = new InstallManager(bundledRelease, outDir);
                    var pkg = new ZipPackage(Path.Combine(dir, package));

                    fixture.ExecuteInstall(dir, pkg).Wait();

                    var filesToLookFor = new[] {
                        "ProjectWithContent\\app-1.0.0.0\\project-with-content.exe",
                        "ProjectWithContent\\app-1.0.0.0\\some-words.txt",
                        "ProjectWithContent\\app-1.0.0.0\\dir\\item-in-subdirectory.txt",
                        "ProjectWithContent\\packages\\RELEASES",
                        "ProjectWithContent\\packages\\ProjectWithContent.1.0.0.0-beta-full.nupkg",
                    };

                    filesToLookFor.ForEach(f => Assert.True(File.Exists(Path.Combine(outDir, f)), "Could not find file: " + f));
                }
                finally
                {
                    Directory.Delete(dir, true);
                }
            }
        }
    }
}
