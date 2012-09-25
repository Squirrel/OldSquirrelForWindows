using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
            using (withFakeInstallDirectory(out dir)) {
                var fixture = new WixUiBootstrapper(events.Object, null, router, null, dir);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                detectComplete.OnNext(new DetectPackageCompleteEventArgs("Foo", packHResultIntoIntEvenThoughItShouldntBeThere(0x80004005), PackageState.Unknown));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(ErrorViewModel));

                router.NavigateAndReset.Execute(RxApp.GetService<IWelcomeViewModel>());
                error.OnNext(new ErrorEventArgs(ErrorType.ExePackage, "Foo", 
                    packHResultIntoIntEvenThoughItShouldntBeThere(0x80004005), "Noope", 0, new string[0], 0));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(ErrorViewModel));
            }
        }

        //
        // DetectPackageComplete
        //
        
        [Fact]
        public void RouteToInstallOnDetectPackageComplete()
        {
            var router = new RoutingState();
            var detectComplete = new Subject<DetectPackageCompleteEventArgs>();
            var error = new Subject<ErrorEventArgs>();

            var events = new Mock<IWiXEvents>();
            events.SetupGet(x => x.DetectPackageCompleteObs).Returns(detectComplete);
            events.SetupGet(x => x.ErrorObs).Returns(error);
            events.SetupGet(x => x.PlanCompleteObs).Returns(Observable.Never<PlanCompleteEventArgs>());
            events.SetupGet(x => x.ApplyCompleteObs).Returns(Observable.Never<ApplyCompleteEventArgs>());

            events.SetupGet(x => x.DisplayMode).Returns(Display.Full);
            events.SetupGet(x => x.Action).Returns(LaunchAction.Install);

            string dir;
            using (withFakeInstallDirectory(out dir)) {
                var fixture = new WixUiBootstrapper(events.Object, null, router, null, dir);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                detectComplete.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(WelcomeViewModel));
            }
        }

        [Fact]
        public void RouteToUninstallOnDetectPackageComplete()
        {
            var router = new RoutingState();
            var detectComplete = new Subject<DetectPackageCompleteEventArgs>();
            var error = new Subject<ErrorEventArgs>();

            var events = new Mock<IWiXEvents>();
            events.SetupGet(x => x.DetectPackageCompleteObs).Returns(detectComplete);
            events.SetupGet(x => x.ErrorObs).Returns(error);
            events.SetupGet(x => x.PlanCompleteObs).Returns(Observable.Never<PlanCompleteEventArgs>());
            events.SetupGet(x => x.ApplyCompleteObs).Returns(Observable.Never<ApplyCompleteEventArgs>());

            events.SetupGet(x => x.DisplayMode).Returns(Display.Full);
            events.SetupGet(x => x.Action).Returns(LaunchAction.Uninstall);

            string dir;
            using (withFakeInstallDirectory(out dir)) {
                var fixture = new WixUiBootstrapper(events.Object, null, router, null, dir);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                detectComplete.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(UninstallingViewModel));
            }
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

        static object gate = 42;
        static IDisposable withFakeInstallDirectory(out string path)
        {
            var ret = Utility.WithTempDirectory(out path);

            const string pkg = "SampleUpdatingApp.1.1.0.0.nupkg";
            File.Copy(IntegrationTestHelper.GetPath("fixtures", pkg), Path.Combine(path, pkg));
            var rp = ReleaseEntry.GenerateFromFile(Path.Combine(path, pkg));
            ReleaseEntry.WriteReleaseFile(new[] {rp}, Path.Combine(path, "RELEASES"));

            // NB: This is a temporary hack. The reason we serialize the tests
            // like this, is to make sure that we don't have two tests registering
            // their Service Locators with RxApp.
            Monitor.Enter(gate);
            return new CompositeDisposable(ret, Disposable.Create(() => Monitor.Exit(gate)));
        }

        static int packHResultIntoIntEvenThoughItShouldntBeThere(uint hr)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(hr), 0);
        }
    }
}