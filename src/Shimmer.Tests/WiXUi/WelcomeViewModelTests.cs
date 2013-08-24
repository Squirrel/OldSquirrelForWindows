using System;
using Moq;
using NuGet;
using ReactiveUI.Routing;
using Shimmer.WiXUi.ViewModels;
using Xunit;

namespace Shimmer.Tests.WiXUi
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

                Assert.Equal("", viewModel.Summary);
            }

            [Fact]
            public void WhenPackageSpecifiesValueItIsDisplayed()
            {
                var package = Mock.Of<IPackage>(p => p.Summary == "My App Summary");

                var viewModel = new WelcomeViewModel(Mock.Of<IScreen>())
                {
                    PackageMetadata = package
                };

                Assert.Equal("My App Summary", viewModel.Summary);
            }
        }
    }
}
