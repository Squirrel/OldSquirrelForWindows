using System.IO;
using System.Linq;
using NSync.Core;
using NSync.Tests.TestHelpers;
using NuGet;
using Xunit;

namespace NSync.Tests.Core
{
    public class ReleasePackageTests : IEnableLogger
    {
        [Fact]
        public void ReleasePackageIntegrationTest()
        {
            var inputPackage = IntegrationTestHelper.GetPath("fixtures", "NSync.Core.1.0.0.0.nupkg");
            var outputPackage = Path.GetTempFileName() + ".nupkg";
            var fixture = new ReleasePackage(inputPackage);

            fixture.CreateReleasePackage(outputPackage);

            var pkg = new ZipPackage(outputPackage);
            pkg.References.Count().ShouldEqual(0);
        }
    }
}