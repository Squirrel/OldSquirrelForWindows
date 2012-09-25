using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using Moq;
using ReactiveUI;
using ReactiveUI.Routing;
using Shimmer.Client.WiXUi;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using Shimmer.WiXUi.ViewModels;
using Xunit;

using ErrorEventArgs = Microsoft.Tools.WindowsInstallerXml.Bootstrapper.ErrorEventArgs;

namespace Shimmer.Tests.WiXUi
{
    public class WiXUiBootstrapperTests
    {
        [Fact]
        public void RouteToErrorViewWhenThingsGoPearShaped()
        {
            var router = new RoutingState();
            var detectComplete = new Subject<DetectPackageCompleteEventArgs>();
            var error = new Subject<ErrorEventArgs>();

            var events = new Mock<IWiXEvents>();
            events.SetupGet(x => x.DetectPackageCompleteObs).Returns(detectComplete);
            events.SetupGet(x => x.ErrorObs).Returns(error);
            events.SetupGet(x => x.PlanCompleteObs).Returns(Observable.Never<PlanCompleteEventArgs>());
            events.SetupGet(x => x.ApplyCompleteObs).Returns(Observable.Never<ApplyCompleteEventArgs>());

            string dir;
            const string pkg = "SampleUpdatingApp.1.1.0.0.nupkg";
            using (Utility.WithTempDirectory(out dir)) {
                File.Copy(IntegrationTestHelper.GetPath("fixtures", pkg), Path.Combine(dir, pkg));
                var rp = ReleaseEntry.GenerateFromFile(Path.Combine(dir, pkg));
                ReleaseEntry.WriteReleaseFile(new[] {rp}, Path.Combine(dir, "RELEASES"));

                var fixture = new WixUiBootstrapper(events.Object, null, router, null, dir);
                detectComplete.OnNext(new DetectPackageCompleteEventArgs("Foo", PackHResultIntoIntEvenThoughItShouldntBeThere(0x80004005), PackageState.Unknown));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(ErrorViewModel));

                router.NavigateAndReset.Execute(RxApp.GetService<IWelcomeViewModel>());
                error.OnNext(new ErrorEventArgs(ErrorType.ExePackage, "Foo", 
                    PackHResultIntoIntEvenThoughItShouldntBeThere(0x80004005), "Noope", 0, new string[0], 0));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(ErrorViewModel));
            }
        }

        //
        // DetectPackageComplete
        //
        
        [Fact]
        public void RouteToInstallOnDetectPackageComplete()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void RouteToUninstallOnDetectPackageComplete()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void IfAppIsAlreadyInstalledRunTheApp()
        {
            throw new NotImplementedException();
        }

        //
        // PlanComplete
        //

        [Fact]
        public void EigenUpdateWithoutUpdateURL()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void EigenUpdateWithUpdateURL()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void UpdateReportsProgress()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void InstallHandlesAccessDenied()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void UninstallRemovesEverything()
        {
            throw new NotImplementedException();
        }

        //
        // Helper methods
        //

        [Fact]
        public void DetermineFxVersionFromPackageTest()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void RegisterExtensionDllsFindsExtensions()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void DefaultTypesShouldntStepOnExtensionRegisteredTypes()
        {
            throw new NotImplementedException();
        }

        int PackHResultIntoIntEvenThoughItShouldntBeThere(uint hr)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(hr), 0);
        }
    }
}