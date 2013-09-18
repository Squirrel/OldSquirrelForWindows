using NuGet;
using Shimmer.Client;
using Shimmer.Client.Extensions;
using Shimmer.Tests.TestHelpers;
using Xunit.Extensions;
using Assert = Xunit.Assert;

namespace Shimmer.Tests.Client
{
    public class PackageExtensionsTests
    {
        [Theory]
        [InlineData("Shimmer.Core.1.0.0.0-full.nupkg", FrameworkVersion.Net40)]
        [InlineData("Caliburn.Micro.1.5.2.nupkg", FrameworkVersion.Net45)]
        public void DetectFrameworkVersion(string packageName, FrameworkVersion expected)
        {
            var path = IntegrationTestHelper.GetPath("fixtures", packageName);

            var zip = new ZipPackage(path);

            Assert.Equal(expected, zip.DetectFrameworkVersion());
        }
    }
}
