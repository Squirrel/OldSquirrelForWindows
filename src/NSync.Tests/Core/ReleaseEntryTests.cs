using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NSync.Core;
using NSync.Tests.TestHelpers;
using Xunit;
using Xunit.Extensions;

namespace NSync.Tests.Core
{
    public class ReleaseEntryTests
    {
        [Theory]
        [InlineData("94689fede03fed7ab59c24337673a27837f0c3ec  MyCoolApp-1.0.nupkg  1004502", "MyCoolApp-1.0.nupkg", 1004502)]
        [InlineData("3a2eadd15dd984e4559f2b4d790ec8badaeb6a39  MyCoolApp-1.1.nupkg  1040561", "MyCoolApp-1.1.nupkg", 1040561)]
        [InlineData("14db31d2647c6d2284882a2e101924a9c409ee67  MyCoolApp-1.1.nupkg.delta  80396", "MyCoolApp-1.1.nupkg.delta", 80396)]
        public void ParseValidReleaseEntryLines(string releaseEntry, string fileName, long fileSize)
        {
            var fixture = ReleaseEntry.ParseReleaseEntry(releaseEntry);
            Assert.Equal(fixture.Filename, fileName);
            Assert.Equal(fixture.Filesize, fileSize);
        }

        [Theory]
        [InlineData("NSync.Core.1.0.0.0.nupkg", 4457, "75255cfd229a1ed1447abe1104f5635e69975d30")]
        [InlineData("NSync.Core.1.1.0.0.nupkg", 15830, "9baf1dbacb09940086c8c62d9a9dbe69fe1f7593")]
        public void GenerateFromFileTest(string name, long size, string sha1)
        {
            var path = IntegrationTestHelper.GetPath("fixtures", name);

            using (var f = File.OpenRead(path)) {
                var fixture = ReleaseEntry.GenerateFromFile(f, "dontcare");
                Assert.Equal(size, fixture.Filesize);
                Assert.Equal(sha1, fixture.SHA1.ToLowerInvariant());
            }
        }
    }
}
