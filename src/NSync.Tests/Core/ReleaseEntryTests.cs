using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NSync.Core;
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
    }
}
