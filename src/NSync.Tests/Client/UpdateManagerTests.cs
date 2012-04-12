using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using NSync.Client;
using NSync.Tests.TestHelpers;
using Xunit;

namespace NSync.Tests.Client
{
    public class UpdateManagerTests
    {
        [Fact]
        public void NewReleasesShouldBeDetected()
        {
            var openPath = new Func<string, Stream>(s => {
                Assert.Equal(s, "RELEASES");
                return File.OpenRead(IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOh"));
            });

            var downloadUrl = new Func<string, IObservable<string>>(url => {
                var path = IntegrationTestHelper.GetPath("fixtures", "RELEASES-OnePointOne");
                return Observable.Return(File.ReadAllText(path, Encoding.UTF8));
            });

            var fixture = new UpdateManager("http://lol", openPath, downloadUrl);
            var result = fixture.CheckForUpdate().First();

            Assert.NotNull(result);
            Assert.Equal("1.1", result.Version);
        }
    }
}
