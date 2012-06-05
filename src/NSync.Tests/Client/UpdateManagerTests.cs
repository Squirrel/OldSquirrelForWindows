using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Moq;
using NSync.Client;
using NSync.Core;
using NSync.Tests.TestHelpers;
using Xunit;

namespace NSync.Tests.Client
{
    public class UpdateManagerTests
    {
        [Fact]
        public void NewReleasesShouldBeDetected()
        {
            string expectedPath = Path.Combine(".", "theApp", "packages", "RELEASES");

            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.Setup(x => x.OpenRead())
                .Returns(File.OpenRead(IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOh")));

            var fs = new Mock<IFileSystemFactory>();
            fs.Setup(x => x.GetFileInfo(expectedPath)).Returns(fileInfo.Object);

            var urlDownloader = new Mock<IUrlDownloader>();
            var dlPath = IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOne");
            urlDownloader.Setup(x => x.DownloadUrl(It.IsAny<string>()))
                .Returns(Observable.Return(File.ReadAllText(dlPath, Encoding.UTF8)));

            var fixture = new UpdateManager("http://lol", "theApp", ".", fs.Object, urlDownloader.Object);
            var result = fixture.CheckForUpdate().First();

            Assert.NotNull(result);
            Assert.Equal(1, result.Version.Major);
            Assert.Equal(1, result.Version.Minor);
        }

        [Fact]
        public void NoReleasesFileMeansWeStartFromScratch()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void NoDirectoryMeansWeStartFromScratch()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void CorruptedReleaseFileMeansWeStartFromScratch()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void ChecksumShouldPassOnValidPackages()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void ChecksumShouldFailIfFilesAreMissing()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void ChecksumShouldFailIfFilesAreBogus()
        {
            throw new NotImplementedException();
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
}
