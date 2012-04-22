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
            var downloadUrl = new Func<string, IObservable<string>>(url => {
                var path = IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOne");
                return Observable.Return(File.ReadAllText(path, Encoding.UTF8));
            });

            string expectedPath = Path.Combine(".", "theApp", "packages", "RELEASES");

            var fileInfo = new Mock<FileInfoBase>();
            fileInfo.Setup(x => x.OpenRead())
                .Returns(File.OpenRead(IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOh")));

            var fs = new AnonFileSystem(_ => null, path => {
                Assert.Equal(expectedPath, path);
                return fileInfo.Object;
            }, _ => null);

            var fixture = new UpdateManager("http://lol", "theApp", ".", fs, downloadUrl);
            var result = fixture.CheckForUpdate().First();

            Assert.NotNull(result);
            Assert.Equal(1, result.Version.Major);
            Assert.Equal(1, result.Version.Minor);
        }
    }
}
