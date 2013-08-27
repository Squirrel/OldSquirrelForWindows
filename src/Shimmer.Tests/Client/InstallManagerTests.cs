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
        [Fact]
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

                fixture.ExecuteInstall(dir, pkg, progress);

                var filesToLookFor = new[] {
                    "SampleUpdatingApp\\app-1.1.0.0\\SampleUpdatingApp.exe",
                    "SampleUpdatingApp\\packages\\RELEASES",
                    "SampleUpdatingApp\\packages\\SampleUpdatingApp.1.1.0.0.nupkg",
                };

                filesToLookFor.All(x => File.Exists(Path.Combine(outDir, x))).ShouldBeTrue();

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

        [Fact(Skip = "TODO")]
        public void UninstallRunsHooks()
        {
            throw new NotImplementedException();
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
    }
}
