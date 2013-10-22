using System;
using Moq;
using NuGet;
using ReactiveUI.Routing;
using Squirrel.WiXUi.ViewModels;
using Xunit;

namespace Squirrel.Tests.WiXUi
{
    public class WelcomeViewModelTests
    {
        public class TheTitleProperty
        {
            [Fact]
            public void WhenNullPackageSpecifiedReturnEmptyString()
            {
                var viewModel = new WelcomeViewModel(Mock.Of<IScreen>()) {
                    PackageMetadata = null
                };

                Assert.Equal("", viewModel.Title);
            }

            [Fact]
            public void WhenPackageSpecifiesATitleUseIt()
            {
                var package = Mock.Of<IPackage>(p => p.Title == "My App Title");

                var viewModel = new WelcomeViewModel(Mock.Of<IScreen>()) {
                    PackageMetadata = package
                };

                Assert.Equal("My App Title", viewModel.Title);
            }

            [Fact]
            public void WhenPackageSpecifiesNullTitleFallbackToId()
            {
                var package = Mock.Of<IPackage>(p => p.Id == "MyApp");

                var viewModel = new WelcomeViewModel(Mock.Of<IScreen>())
                {
                    PackageMetadata = package
                };

                Assert.Equal("MyApp", viewModel.Title);
            }

            // if <title> not specified in NuGet package, we should use <id> here
            // citation: http://docs.nuget.org/docs/reference/nuspec-reference#Metadata_Section
            [Fact]
            public void WhenPackageSpecifiesNoTitleFallbackToId()
            {
                var package = Mock.Of<IPackage>(p => p.Id == "MyApp" && p.Title == "");

                var viewModel = new WelcomeViewModel(Mock.Of<IScreen>()) {
                    PackageMetadata = package
                };

                Assert.Equal("MyApp", viewModel.Title);
            }
        }

        public class TheSummaryProperty
        {
            [Fact]
            public void WhenNullPackageSpecifiedReturnEmptyString()
            {
                var viewModel = new WelcomeViewModel(Mock.Of<IScreen>())
                {
                    PackageMetadata = null
                };

                Assert.Equal("", viewModel.Description);
            }

            [Fact]
            public void WhenPackageSpecifiesValueItIsDisplayed()
            {
                var package = Mock.Of<IPackage>(p => p.Description == "My App Description");

                var viewModel = new WelcomeViewModel(Mock.Of<IScreen>())
                {
                    PackageMetadata = package
                };

                Assert.Equal("My App Description", viewModel.Description);
            }
        }
    }
}
