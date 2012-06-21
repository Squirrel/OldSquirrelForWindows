using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using Moq;
using NSync.Client;
using NSync.Core;
using NSync.Tests.TestHelpers;
using NuGet;
using Xunit;

namespace NSync.Tests.Client
{
    public class UpdateManagerTests
    {
        public class CheckForUpdateTests
        {
            [Fact]
            public void NewReleasesShouldBeDetected()
            {
                string localReleasesFile = Path.Combine(".", "theApp", "packages", "RELEASES");

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.OpenRead())
                    .Returns(File.OpenRead(IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOh")));

                var fs = new Mock<IFileSystemFactory>();
                fs.Setup(x => x.GetFileInfo(localReleasesFile)).Returns(fileInfo.Object);

                var urlDownloader = new Mock<IUrlDownloader>();
                var dlPath = IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOne");
                urlDownloader.Setup(x => x.DownloadUrl(It.IsAny<string>()))
                    .Returns(Observable.Return(File.ReadAllText(dlPath, Encoding.UTF8)));

                var fixture = new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object);
                var result = default(UpdateInfo);

                using (fixture.AcquireUpdateLock()) {
                    result = fixture.CheckForUpdate().First();
                }

                Assert.NotNull(result);
                Assert.Equal(1, result.ReleasesToApply.Single().Version.Major);
                Assert.Equal(1, result.ReleasesToApply.Single().Version.Minor);
            }

            [Fact]
            public void NoLocalReleasesFileMeansWeStartFromScratch()
            {
                string localPackagesDir = Path.Combine(".", "theApp", "packages");
                string localReleasesFile = Path.Combine(localPackagesDir, "RELEASES");

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.Exists).Returns(false);

                var dirInfo = new Mock<DirectoryInfoBase>();
                dirInfo.Setup(x => x.Exists).Returns(true);

                var fs = new Mock<IFileSystemFactory>();
                fs.Setup(x => x.GetFileInfo(localReleasesFile)).Returns(fileInfo.Object);
                fs.Setup(x => x.CreateDirectoryRecursive(localPackagesDir)).Verifiable();
                fs.Setup(x => x.DeleteDirectoryRecursive(localPackagesDir)).Verifiable();
                fs.Setup(x => x.GetDirectoryInfo(localPackagesDir)).Returns(dirInfo.Object);

                var urlDownloader = new Mock<IUrlDownloader>();
                var dlPath = IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOne");
                urlDownloader.Setup(x => x.DownloadUrl(It.IsAny<string>()))
                    .Returns(Observable.Return(File.ReadAllText(dlPath, Encoding.UTF8)));

                var fixture = new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object);
                using (fixture.AcquireUpdateLock()) {
                    fixture.CheckForUpdate().First();
                }

                fs.Verify(x => x.CreateDirectoryRecursive(localPackagesDir), Times.Once());
                fs.Verify(x => x.DeleteDirectoryRecursive(localPackagesDir), Times.Once());
            }

            [Fact]
            public void NoLocalDirectoryMeansWeStartFromScratch()
            {
                string localPackagesDir = Path.Combine(".", "theApp", "packages");
                string localReleasesFile = Path.Combine(localPackagesDir, "RELEASES");

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.Exists).Returns(false);

                var dirInfo = new Mock<DirectoryInfoBase>();
                dirInfo.Setup(x => x.Exists).Returns(false);

                var fs = new Mock<IFileSystemFactory>();
                fs.Setup(x => x.GetFileInfo(localReleasesFile)).Returns(fileInfo.Object);
                fs.Setup(x => x.CreateDirectoryRecursive(It.IsAny<string>())).Verifiable();
                fs.Setup(x => x.GetDirectoryInfo(localPackagesDir)).Returns(dirInfo.Object);

                var urlDownloader = new Mock<IUrlDownloader>();
                var dlPath = IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOne");
                urlDownloader.Setup(x => x.DownloadUrl(It.IsAny<string>()))
                    .Returns(Observable.Return(File.ReadAllText(dlPath, Encoding.UTF8)));

                var fixture = new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object);
                using (fixture.AcquireUpdateLock()) {
                    fixture.CheckForUpdate().First();
                }

                fs.Verify(x => x.CreateDirectoryRecursive(localPackagesDir), Times.Once());
            }

            [Fact]
            public void CorruptedReleaseFileMeansWeStartFromScratch()
            {
                string localPackagesDir = Path.Combine(".", "theApp", "packages");
                string localReleasesFile = Path.Combine(localPackagesDir, "RELEASES");

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.Exists).Returns(true);
                fileInfo.Setup(x => x.OpenRead())
                    .Returns(new MemoryStream(Encoding.UTF8.GetBytes("lol this isn't right")));

                var dirInfo = new Mock<DirectoryInfoBase>();
                dirInfo.Setup(x => x.Exists).Returns(true);

                var fs = new Mock<IFileSystemFactory>();
                fs.Setup(x => x.GetFileInfo(localReleasesFile)).Returns(fileInfo.Object);
                fs.Setup(x => x.CreateDirectoryRecursive(localPackagesDir)).Verifiable();
                fs.Setup(x => x.DeleteDirectoryRecursive(localPackagesDir)).Verifiable();
                fs.Setup(x => x.GetDirectoryInfo(localPackagesDir)).Returns(dirInfo.Object);

                var urlDownloader = new Mock<IUrlDownloader>();
                var dlPath = IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOne");
                urlDownloader.Setup(x => x.DownloadUrl(It.IsAny<string>()))
                    .Returns(Observable.Return(File.ReadAllText(dlPath, Encoding.UTF8)));

                var fixture = new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object);
                using (fixture.AcquireUpdateLock()) {
                    fixture.CheckForUpdate().First();
                }

                fs.Verify(x => x.CreateDirectoryRecursive(localPackagesDir), Times.Once());
                fs.Verify(x => x.DeleteDirectoryRecursive(localPackagesDir), Times.Once());
            }

            [Fact]
            public void CorruptRemoteFileShouldThrowOnCheck()
            {
                string localPackagesDir = Path.Combine(".", "theApp", "packages");
                string localReleasesFile = Path.Combine(localPackagesDir, "RELEASES");

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.Exists).Returns(false);

                var dirInfo = new Mock<DirectoryInfoBase>();
                dirInfo.Setup(x => x.Exists).Returns(true);

                var fs = new Mock<IFileSystemFactory>();
                fs.Setup(x => x.GetFileInfo(localReleasesFile)).Returns(fileInfo.Object);
                fs.Setup(x => x.CreateDirectoryRecursive(localPackagesDir)).Verifiable();
                fs.Setup(x => x.DeleteDirectoryRecursive(localPackagesDir)).Verifiable();
                fs.Setup(x => x.GetDirectoryInfo(localPackagesDir)).Returns(dirInfo.Object);

                var urlDownloader = new Mock<IUrlDownloader>();
                urlDownloader.Setup(x => x.DownloadUrl(It.IsAny<string>()))
                    .Returns(Observable.Return("lol this isn't right"));

                var fixture = new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object);

                Assert.Throws<Exception>(() => fixture.CheckForUpdate().First());
            }

            [Fact]
            public void IfLocalVersionGreaterThanRemoteWeRollback()
            {
                throw new NotImplementedException();
            }

            [Fact]
            public void IfLocalAndRemoteAreEqualThenDoNothing()
            {
                throw new NotImplementedException();
            }
        }

        public class DownloadReleasesTests
        {
            [Fact]
            public void ChecksumShouldPassOnValidPackages()
            {
                var filename = "NSync.Core.1.0.0.0.nupkg";
                var nuGetPkg = IntegrationTestHelper.GetPath("fixtures", filename);
                var fs = new Mock<IFileSystemFactory>();
                var urlDownloader = new Mock<IUrlDownloader>();

                ReleaseEntry entry;
                using (var f = File.OpenRead(nuGetPkg)) {
                    entry = ReleaseEntry.GenerateFromFile(f, filename);
                }

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.OpenRead()).Returns(File.OpenRead(nuGetPkg));
                fileInfo.Setup(x => x.Exists).Returns(true);
                fileInfo.Setup(x => x.Length).Returns(new FileInfo(nuGetPkg).Length);

                fs.Setup(x => x.GetFileInfo(Path.Combine(".", "theApp", "packages", filename))).Returns(fileInfo.Object);

                var fixture = ExposedObject.From(
                    new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object));

                IObservable<Unit> result = fixture.checksumPackage(entry);
                result.First();
            }

            [Fact]
            public void ChecksumShouldFailIfFilesAreMissing()
            {
                var filename = "NSync.Core.1.0.0.0.nupkg";
                var nuGetPkg = IntegrationTestHelper.GetPath("fixtures", filename);
                var fs = new Mock<IFileSystemFactory>();
                var urlDownloader = new Mock<IUrlDownloader>();

                ReleaseEntry entry;
                using (var f = File.OpenRead(nuGetPkg)) {
                    entry = ReleaseEntry.GenerateFromFile(f, filename);
                }

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.OpenRead()).Returns(File.OpenRead(nuGetPkg));
                fileInfo.Setup(x => x.Exists).Returns(false);

                fs.Setup(x => x.GetFileInfo(Path.Combine(".", "theApp", "packages", filename))).Returns(fileInfo.Object);

                var fixture = ExposedObject.From(
                    new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object));

                IObservable<Unit> result = fixture.checksumPackage(entry);
                Assert.Throws<Exception>(() => result.First());
            }

            [Fact]
            public void ChecksumShouldFailIfFilesAreBogus()
            {
                var filename = "NSync.Core.1.0.0.0.nupkg";
                var nuGetPkg = IntegrationTestHelper.GetPath("fixtures", filename);
                var fs = new Mock<IFileSystemFactory>();
                var urlDownloader = new Mock<IUrlDownloader>();

                ReleaseEntry entry;
                using (var f = File.OpenRead(nuGetPkg)) {
                    entry = ReleaseEntry.GenerateFromFile(f, filename);
                }

                var fileInfo = new Mock<FileInfoBase>();
                fileInfo.Setup(x => x.OpenRead()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("Lol broken")));
                fileInfo.Setup(x => x.Exists).Returns(true);
                fileInfo.Setup(x => x.Length).Returns(new FileInfo(nuGetPkg).Length);
                fileInfo.Setup(x => x.Delete()).Verifiable();

                fs.Setup(x => x.GetFileInfo(Path.Combine(".", "theApp", "packages", filename))).Returns(fileInfo.Object);

                var fixture = ExposedObject.From(
                    new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object));

                bool shouldDie = true;
                IObservable<Unit> result = fixture.checksumPackage(entry);
                try {
                    result.First();
                } catch (Exception ex) {
                    shouldDie = false;
                }

                shouldDie.ShouldBeFalse();
                fileInfo.Verify(x => x.Delete(), Times.Once());
            }

            [Fact]
            public void DownloadReleasesFromHttpServerIntegrationTest()
            {
                throw new NotImplementedException();
            }

            [Fact]
            public void DownloadReleasesFromFileDirectoryIntegrationTest()
            {
                throw new NotImplementedException();
            }
        }

        public class ApplyReleasesTests : IEnableLogger
        {
            [Fact]
            public void ApplyReleasesWithOneReleaseFile()
            {
                string tempDir;

                using (Utility.WithTempDirectory(out tempDir)) {
                    Directory.CreateDirectory(Path.Combine(tempDir, "theApp"));
                    Directory.CreateDirectory(Path.Combine(tempDir, "theApp", "packages"));

                    new[] {
                        "NSync.Core.1.0.0.0-full.nupkg",
                        "NSync.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x => File.Copy(IntegrationTestHelper.GetPath("fixtures", x), Path.Combine(tempDir, "theApp", "packages", x)));

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager("http://lol", "theApp", tempDir, null, urlDownloader.Object);

                    var baseEntry = ReleaseEntry.GenerateFromFile(Path.Combine(tempDir, "theApp", "packages", "NSync.Core.1.0.0.0-full.nupkg"));
                    var latestFullEntry = ReleaseEntry.GenerateFromFile(Path.Combine(tempDir, "theApp", "packages", "NSync.Core.1.1.0.0-full.nupkg"));

                    var updateInfo = UpdateInfo.Create(baseEntry, new[] { latestFullEntry });
                    updateInfo.ReleasesToApply.Contains(latestFullEntry).ShouldBeTrue();

                    using (fixture.AcquireUpdateLock()) {
                        fixture.ApplyReleases(updateInfo).First();
                    }

                    var filesToFind = new[] {
                        new {Name = "NLog.dll", Version = new Version("2.0.0.0")},
                        new {Name = "NSync.Core.dll", Version = new Version("1.1.0.0")},
                        new {Name = "Ionic.Zip.dll", Version = new Version("1.9.1.8")},
                    };

                    filesToFind.ForEach(x => {
                        var path = Path.Combine(tempDir, "theApp", "app-1.1.0.0", x.Name);
                        this.Log().Info("Looking for {0}", path);
                        File.Exists(path).ShouldBeTrue();

                        var vi = FileVersionInfo.GetVersionInfo(path);
                        var verInfo = new Version(vi.FileVersion ?? "1.0.0.0");
                        x.Version.ShouldEqual(verInfo);
                    });
                }
            }

            [Fact]
            public void ApplyReleasesWithDeltaReleases()
            {
                string tempDir;

                using (Utility.WithTempDirectory(out tempDir)) {
                    Directory.CreateDirectory(Path.Combine(tempDir, "theApp"));
                    Directory.CreateDirectory(Path.Combine(tempDir, "theApp", "packages"));

                    new[] {
                        "NSync.Core.1.0.0.0-full.nupkg",
                        "NSync.Core.1.1.0.0-delta.nupkg",
                        "NSync.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x => File.Copy(IntegrationTestHelper.GetPath("fixtures", x), Path.Combine(tempDir, "theApp", "packages", x)));

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager("http://lol", "theApp", tempDir, null, urlDownloader.Object);

                    var baseEntry = ReleaseEntry.GenerateFromFile(Path.Combine(tempDir, "theApp", "packages", "NSync.Core.1.0.0.0-full.nupkg"));
                    var deltaEntry = ReleaseEntry.GenerateFromFile(Path.Combine(tempDir, "theApp", "packages", "NSync.Core.1.1.0.0-delta.nupkg"));
                    var latestFullEntry = ReleaseEntry.GenerateFromFile(Path.Combine(tempDir, "theApp", "packages", "NSync.Core.1.1.0.0-full.nupkg"));

                    var updateInfo = UpdateInfo.Create(baseEntry, new[] { deltaEntry, latestFullEntry });
                    updateInfo.ReleasesToApply.Contains(deltaEntry).ShouldBeTrue();

                    using (fixture.AcquireUpdateLock()) {
                        fixture.ApplyReleases(updateInfo).First();
                    }

                    var filesToFind = new[] {
                        new {Name = "NLog.dll", Version = new Version("2.0.0.0")},
                        new {Name = "NSync.Core.dll", Version = new Version("1.1.0.0")},
                        new {Name = "Ionic.Zip.dll", Version = new Version("1.9.1.8")},
                    };

                    filesToFind.ForEach(x => {
                        var path = Path.Combine(tempDir, "theApp", "app-1.1.0.0", x.Name);
                        this.Log().Info("Looking for {0}", path);
                        File.Exists(path).ShouldBeTrue();

                        var vi = FileVersionInfo.GetVersionInfo(path);
                        var verInfo = new Version(vi.FileVersion ?? "1.0.0.0");
                        x.Version.ShouldEqual(verInfo);
                    });
                }
            }

            [Fact]
            public void CreateFullPackagesFromDeltaSmokeTest()
            {
                string tempDir;
                using (Utility.WithTempDirectory(out tempDir)) {
                    Directory.CreateDirectory(Path.Combine(tempDir, "theApp", "packages"));

                    new[] {
                        "NSync.Core.1.0.0.0-full.nupkg",
                        "NSync.Core.1.1.0.0-delta.nupkg"
                    }.ForEach(x => File.Copy(IntegrationTestHelper.GetPath("fixtures", x), Path.Combine(tempDir, "theApp", "packages", x)));

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager("http://lol", "theApp", tempDir, null, urlDownloader.Object);

                    var baseEntry = ReleaseEntry.GenerateFromFile(Path.Combine(tempDir, "theApp", "packages", "NSync.Core.1.0.0.0-full.nupkg"));
                    var deltaEntry = ReleaseEntry.GenerateFromFile(Path.Combine(tempDir, "theApp", "packages", "NSync.Core.1.1.0.0-delta.nupkg"));

                    var resultObs = (IObservable<ReleaseEntry>)fixture.GetType().GetMethod("createFullPackagesFromDeltas", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(fixture, new object[] { new[] {deltaEntry}, baseEntry });

                    var result = resultObs.First();
                    var zp = new ZipPackage(Path.Combine(tempDir, "theApp", "packages", result.Filename));

                    zp.Version.ToString().ShouldEqual("1.1.0.0");
                }
            }

            [Fact]
            public void ShouldCallAppUninstallOnTheOldVersion()
            {
                throw new NotImplementedException();
            }

            [Fact]
            public void CallAppInstallOnTheJustInstalledVersion()
            {
                throw new NotImplementedException();
            }

            [Fact]
            public void ShouldCreateAppShortcutsBasedOnClientExe()
            {
                throw new NotImplementedException();
            }

            [Fact]
            public void DeletedShortcutsShouldntBeRecreatedOnUpgrade()
            {
                throw new NotImplementedException();
            }
        }

        public class UpdateLocalReleasesTests
        {
            [Fact]
            public void UpdateLocalReleasesSmokeTest()
            {
                string tempDir;
                using (Utility.WithTempDirectory(out tempDir)) {
                    var packageDir = Directory.CreateDirectory(Path.Combine(tempDir, "theApp", "packages"));

                    new[] {
                        "NSync.Core.1.0.0.0-full.nupkg",
                        "NSync.Core.1.1.0.0-delta.nupkg",
                        "NSync.Core.1.1.0.0-full.nupkg",
                    }.ForEach(x => File.Copy(IntegrationTestHelper.GetPath("fixtures", x), Path.Combine(tempDir, "theApp", "packages", x)));

                    var urlDownloader = new Mock<IUrlDownloader>();
                    var fixture = new UpdateManager("http://lol", "theApp", tempDir, null, urlDownloader.Object);

                    using (fixture.AcquireUpdateLock()) {
                        fixture.UpdateLocalReleasesFile().First();
                    }

                    var releasePath = Path.Combine(packageDir.FullName, "RELEASES");
                    File.Exists(releasePath).ShouldBeTrue();

                    var entries = ReleaseEntry.ParseReleaseFile(releasePath);
                    entries.Count().ShouldEqual(3);
                }
            }
        }
    }
}
