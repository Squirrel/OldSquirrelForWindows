using System.Reflection;
using CreateReleasePackage;
using NuGet;
using ReactiveUI;
using Squirrel.Tests.TestHelpers;
using Xunit;

namespace Squirrel.Tests.Release
{
    public class ParseCommandsTests : IEnableLogger
    {
        [Fact]
        public void NuspecMissingRequiredFieldsThrowsExceptions()
        {
            var path = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.1.0.0.nupkg");
            var zp = new ZipPackage(path);

            //Use reflection to grab an instance of the private method
            var checkIfNuspecHasRequiredFields = typeof(ParseCommands).GetMethod("checkIfNuspecHasRequiredFields",
                BindingFlags.Static | BindingFlags.NonPublic);

            zp.Id = "";
            //Blank id exception
            Assert.Throws<TargetInvocationException>(() => checkIfNuspecHasRequiredFields.Invoke(null, new object[] { zp, "Path" }));

            zp.Id = "K";
            //zp.Version = "";
            //TODO: Test a blank version somehow
            
            //zp.Version = "1.0";
            zp.Authors = new string[0];
            //Blank authors exception
            Assert.Throws<TargetInvocationException>(() => checkIfNuspecHasRequiredFields.Invoke(null, new object[] { zp, "Path" }));

            zp.Authors = new[] { "AuthorName" };
            zp.Description = "";
            
            //Blank description exception
            Assert.Throws<TargetInvocationException>(() => checkIfNuspecHasRequiredFields.Invoke(null, new object[] { zp, "Path" }));
        }
    }
}
