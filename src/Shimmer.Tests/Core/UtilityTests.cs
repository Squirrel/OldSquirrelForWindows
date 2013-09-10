using System;
using System.IO;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using Xunit;

namespace Shimmer.Tests.Core
{
    public class UtilityTests
    {
        [Fact]
        public void ShaCheckShouldBeCaseInsensitive()
        {
            var sha1FromExternalTool = "75255cfd229a1ed1447abe1104f5635e69975d30";
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "Shimmer.Core.1.0.0.0.nupkg");
            var stream = File.OpenRead(inputPackage);
            var sha1 = Utility.CalculateStreamSHA1(stream);

            Assert.NotEqual(sha1FromExternalTool, sha1);
            Assert.Equal(sha1FromExternalTool, sha1, StringComparer.OrdinalIgnoreCase);
        }
    }
}
