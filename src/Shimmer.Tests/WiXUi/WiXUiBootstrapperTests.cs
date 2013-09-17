using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Ionic.Zip;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using Moq;
using NuGet;
using ReactiveUI;
using ReactiveUI.Routing;
using Shimmer.Client.WiXUi;
using Shimmer.Core;
using Shimmer.Tests.TestHelpers;
using Shimmer.WiXUi.ViewModels;
using TinyIoC;
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

            events.SetupGet(x => x.Engine).Returns(Mock.Of<IEngine>());

            string dir;
            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir)) {
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
            events.SetupGet(x => x.Engine).Returns(Mock.Of<IEngine>());

            string dir;
            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir)) {
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

            var engine = new Mock<IEngine>();
            engine.Setup(x => x.Plan(LaunchAction.Uninstall)).Verifiable();
            events.SetupGet(x => x.Engine).Returns(engine.Object);

            string dir;
            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir)) {
                var fixture = new WixUiBootstrapper(events.Object, null, router, null, dir);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                detectComplete.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(UninstallingViewModel));
                engine.Verify(x => x.Plan(LaunchAction.Uninstall), Times.Once());
            }
        }

        [Fact]
        public void IfAppIsAlreadyInstalledRunTheApp()
        {
            string dir;
            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir)) {

                // install version 1
                var firstRouter = new RoutingState();
                var firstDetectPackage = new Subject<DetectPackageCompleteEventArgs>();
                var firstPlanComplete = new Subject<PlanCompleteEventArgs>();
                var firstApplyComplete = new Subject<ApplyCompleteEventArgs>();
                var firstError = new Subject<ErrorEventArgs>();

                var firstEvents = new Mock<IWiXEvents>();
                firstEvents.SetupGet(x => x.DetectPackageCompleteObs).Returns(firstDetectPackage);
                firstEvents.SetupGet(x => x.ErrorObs).Returns(firstError);
                firstEvents.SetupGet(x => x.PlanCompleteObs).Returns(firstPlanComplete);
                firstEvents.SetupGet(x => x.ApplyCompleteObs).Returns(firstApplyComplete);

                firstEvents.SetupGet(x => x.DisplayMode).Returns(Display.Full);
                firstEvents.SetupGet(x => x.Action).Returns(LaunchAction.Install);
                firstEvents.SetupGet(x => x.Engine).Returns(Mock.Of<IEngine>());

                var firstFixture = new WixUiBootstrapper(firstEvents.Object, null, firstRouter, null, dir);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                firstDetectPackage.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                var viewModel = firstRouter.GetCurrentViewModel() as WelcomeViewModel;
                viewModel.ShouldProceed.Execute(null);

                firstPlanComplete.OnNext(new PlanCompleteEventArgs(0));

                // TODO: wait for *stuff*

                firstApplyComplete.OnNext(new ApplyCompleteEventArgs(0, ApplyRestart.None));

                // install version 1 again

                var secondRouter = new RoutingState();
                var secondDetectPackage = new Subject<DetectPackageCompleteEventArgs>();
                var secondPlanComplete = new Subject<PlanCompleteEventArgs>();
                var secondApplyComplete = new Subject<ApplyCompleteEventArgs>();
                var secondError = new Subject<ErrorEventArgs>();

                var secondEvents = new Mock<IWiXEvents>();
                secondEvents.SetupGet(x => x.DetectPackageCompleteObs).Returns(secondDetectPackage);
                secondEvents.SetupGet(x => x.ErrorObs).Returns(secondError);
                secondEvents.SetupGet(x => x.PlanCompleteObs).Returns(secondPlanComplete);
                secondEvents.SetupGet(x => x.ApplyCompleteObs).Returns(secondApplyComplete);

                secondEvents.SetupGet(x => x.DisplayMode).Returns(Display.Full);
                secondEvents.SetupGet(x => x.Action).Returns(LaunchAction.Install);
                secondEvents.SetupGet(x => x.Engine).Returns(Mock.Of<IEngine>());

                // install version 1 again
                var secondFixture = new WixUiBootstrapper(secondEvents.Object, null, secondRouter, null, dir);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                secondDetectPackage.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                viewModel = secondRouter.GetCurrentViewModel() as WelcomeViewModel;
                viewModel.ShouldProceed.Execute(null);

                secondPlanComplete.OnNext(new PlanCompleteEventArgs(0));

                // TODO: wait for *stuff*

                secondApplyComplete.OnNext(new ApplyCompleteEventArgs(0, ApplyRestart.None));

                Assert.False(true);
            }
        }

        //
        // PlanComplete
        //

        //
        // Helper methods
        //

        [Fact(Skip = "TODO")]
        public void DetermineFxVersionFromPackageTest()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "TODO")]
        public void RegisterExtensionDllsFindsExtensions()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "TODO")]
        public void DefaultTypesShouldntStepOnExtensionRegisteredTypes()
        {
            throw new NotImplementedException();
        }

        static object gate = 42;

        static int packHResultIntoIntEvenThoughItShouldntBeThere(uint hr)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(hr), 0);
        }
    }
}