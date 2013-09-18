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
using System.Windows;
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

                Func<uint, int> convertHResult = hr => BitConverter.ToInt32(BitConverter.GetBytes(hr), 0);

                detectComplete.OnNext(new DetectPackageCompleteEventArgs("Foo", convertHResult(0x80004005), PackageState.Unknown));

                router.GetCurrentViewModel().GetType().ShouldEqual(typeof(ErrorViewModel));

                router.NavigateAndReset.Execute(RxApp.GetService<IWelcomeViewModel>());
                error.OnNext(new ErrorEventArgs(ErrorType.ExePackage, "Foo",
                    convertHResult(0x80004005), "Noope", 0, new string[0], 0));

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
        public void CanInstallToACustomFolder()
        {
            string dir, targetRootDirectory;
            using (Utility.WithTempDirectory(out targetRootDirectory))
            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir))
            {
                // install version 1
                var firstKernel = new TinyIoCContainer();
                var firstFactory = new Mock<IProcessFactory>();
                firstKernel.Register(firstFactory.Object);

                var firstRouter = new RoutingState();
                var firstDetectPackage = new Subject<DetectPackageCompleteEventArgs>();
                var firstPlanComplete = new Subject<PlanCompleteEventArgs>();
                var firstApplyComplete = new Subject<ApplyCompleteEventArgs>();
                var firstError = new Subject<ErrorEventArgs>();
                var firstEngine = new Mock<IEngine>();

                var firstEvents = new Mock<IWiXEvents>();
                firstEvents.SetupGet(x => x.DetectPackageCompleteObs).Returns(firstDetectPackage);
                firstEvents.SetupGet(x => x.ErrorObs).Returns(firstError);
                firstEvents.SetupGet(x => x.PlanCompleteObs).Returns(firstPlanComplete);
                firstEvents.SetupGet(x => x.ApplyCompleteObs).Returns(firstApplyComplete);
                firstEvents.SetupGet(x => x.Engine).Returns(firstEngine.Object);

                firstEvents.SetupGet(x => x.DisplayMode).Returns(Display.Full);
                firstEvents.SetupGet(x => x.Action).Returns(LaunchAction.Install);

                var firstFixture = new WixUiBootstrapper(firstEvents.Object, firstKernel, firstRouter, null, dir, targetRootDirectory);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                // initialize the install process
                firstDetectPackage.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                // navigate to the next VM
                var viewModel = firstRouter.GetCurrentViewModel() as WelcomeViewModel;
                viewModel.ShouldProceed.Execute(null);

                // signal to start the install
                firstPlanComplete.OnNext(new PlanCompleteEventArgs(0));

                // wait until install is complete
                firstEngine.WaitUntil(e => e.Apply(It.IsAny<IntPtr>()));

                // now signal it's completed
                firstApplyComplete.OnNext(new ApplyCompleteEventArgs(0, ApplyRestart.None));

                Assert.True(Directory.Exists(Path.Combine(targetRootDirectory, "SampleUpdatingApp", "app-1.1.0.0")));
            }
        }


        [Fact]
        public void IfAppIsAlreadyInstalledRunTheApp()
        {
            string dir, targetRootDirectory;
            using (Utility.WithTempDirectory(out targetRootDirectory))
            using (IntegrationTestHelper.WithFakeInstallDirectory(out dir))
            {
                // install version 1
                var firstKernel = new TinyIoCContainer();
                var firstFactory = new Mock<IProcessFactory>();
                firstKernel.Register(firstFactory.Object);

                var firstRouter = new RoutingState();
                var firstDetectPackage = new Subject<DetectPackageCompleteEventArgs>();
                var firstPlanComplete = new Subject<PlanCompleteEventArgs>();
                var firstApplyComplete = new Subject<ApplyCompleteEventArgs>();
                var firstError = new Subject<ErrorEventArgs>();
                var firstEngine = new Mock<IEngine>();

                var firstEvents = new Mock<IWiXEvents>();
                firstEvents.SetupGet(x => x.DetectPackageCompleteObs).Returns(firstDetectPackage);
                firstEvents.SetupGet(x => x.ErrorObs).Returns(firstError);
                firstEvents.SetupGet(x => x.PlanCompleteObs).Returns(firstPlanComplete);
                firstEvents.SetupGet(x => x.ApplyCompleteObs).Returns(firstApplyComplete);
                firstEvents.SetupGet(x => x.Engine).Returns(firstEngine.Object);

                firstEvents.SetupGet(x => x.DisplayMode).Returns(Display.Full);
                firstEvents.SetupGet(x => x.Action).Returns(LaunchAction.Install);

                var firstFixture = new WixUiBootstrapper(firstEvents.Object, firstKernel, firstRouter, null, dir, targetRootDirectory);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                // initialize the install process
                firstDetectPackage.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                // navigate to the next VM
                var viewModel = firstRouter.GetCurrentViewModel() as WelcomeViewModel;
                viewModel.ShouldProceed.Execute(null);

                // signal to start the install
                firstPlanComplete.OnNext(new PlanCompleteEventArgs(0));

                // wait until install is complete
                firstEngine.WaitUntil(e => e.Apply(It.IsAny<IntPtr>()));

                // now signal it's completed
                firstApplyComplete.OnNext(new ApplyCompleteEventArgs(0, ApplyRestart.None));

                // we expect that it opens the main exe
                firstFactory.Verify(p => p.Start(It.IsAny<string>()), Times.Once());

                // install version 1 again
                var secondKernel = new TinyIoCContainer();
                var secondFactory = new Mock<IProcessFactory>();
                secondKernel.Register(secondFactory.Object);

                var secondRouter = new RoutingState();
                var secondDetectPackage = new Subject<DetectPackageCompleteEventArgs>();
                var secondPlanComplete = new Subject<PlanCompleteEventArgs>();
                var secondApplyComplete = new Subject<ApplyCompleteEventArgs>();
                var secondError = new Subject<ErrorEventArgs>();
                var secondEngine = new Mock<IEngine>();

                var secondEvents = new Mock<IWiXEvents>();
                secondEvents.SetupGet(x => x.DetectPackageCompleteObs).Returns(secondDetectPackage);
                secondEvents.SetupGet(x => x.ErrorObs).Returns(secondError);
                secondEvents.SetupGet(x => x.PlanCompleteObs).Returns(secondPlanComplete);
                secondEvents.SetupGet(x => x.ApplyCompleteObs).Returns(secondApplyComplete);
                secondEvents.SetupGet(x => x.Engine).Returns(secondEngine.Object);

                secondEvents.SetupGet(x => x.DisplayMode).Returns(Display.Full);
                secondEvents.SetupGet(x => x.Action).Returns(LaunchAction.Install);

                var secondFixture = new WixUiBootstrapper(secondEvents.Object, secondKernel, secondRouter, null, dir, targetRootDirectory);
                RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                // initialize the install process
                secondDetectPackage.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                // navigate to the next VM
                viewModel = secondRouter.GetCurrentViewModel() as WelcomeViewModel;
                viewModel.ShouldProceed.Execute(null);

                // signal to start the install
                secondPlanComplete.OnNext(new PlanCompleteEventArgs(0));

                // wait until install is complete
                secondEngine.WaitUntil(e => e.Apply(It.IsAny<IntPtr>()));

                // now signal it's completed
                secondApplyComplete.OnNext(new ApplyCompleteEventArgs(0, ApplyRestart.None));

                var folder = Path.Combine(targetRootDirectory, "SampleUpdatingApp", "app-1.1.0.0");
                Assert.True(Directory.Exists(folder));

                var exe = Path.Combine(folder, "SampleUpdatingApp.exe");

                // we expect that it opens the main exe again
                secondFactory.Verify(
                    p => p.Start(exe),
                    Times.Once(),
                    "We expect a process to be executed here, but it ain't...");
            }
        }

        [Fact]
        public void OnUpgradeTheNewExecutableRuns()
        {
            string dir, targetRootDirectory;
            using (Utility.WithTempDirectory(out targetRootDirectory)) {
                using (IntegrationTestHelper.WithFakeInstallDirectory("SampleUpdatingApp.1.0.0.0.nupkg", out dir)) {
                    // install version 1
                    var firstKernel = new TinyIoCContainer();
                    var firstFactory = new Mock<IProcessFactory>();
                    firstKernel.Register(firstFactory.Object);

                    var firstRouter = new RoutingState();
                    var firstDetectPackage = new Subject<DetectPackageCompleteEventArgs>();
                    var firstPlanComplete = new Subject<PlanCompleteEventArgs>();
                    var firstApplyComplete = new Subject<ApplyCompleteEventArgs>();
                    var firstError = new Subject<ErrorEventArgs>();
                    var firstEngine = new Mock<IEngine>();

                    var firstEvents = new Mock<IWiXEvents>();
                    firstEvents.SetupGet(x => x.DetectPackageCompleteObs).Returns(firstDetectPackage);
                    firstEvents.SetupGet(x => x.ErrorObs).Returns(firstError);
                    firstEvents.SetupGet(x => x.PlanCompleteObs).Returns(firstPlanComplete);
                    firstEvents.SetupGet(x => x.ApplyCompleteObs).Returns(firstApplyComplete);
                    firstEvents.SetupGet(x => x.Engine).Returns(firstEngine.Object);

                    firstEvents.SetupGet(x => x.DisplayMode).Returns(Display.Full);
                    firstEvents.SetupGet(x => x.Action).Returns(LaunchAction.Install);

                    var firstFixture = new WixUiBootstrapper(firstEvents.Object, firstKernel, firstRouter, null, dir,
                        targetRootDirectory);
                    RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                    // initialize the install process
                    firstDetectPackage.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                    // navigate to the next VM
                    var viewModel = firstRouter.GetCurrentViewModel() as WelcomeViewModel;
                    viewModel.ShouldProceed.Execute(null);

                    // signal to start the install
                    firstPlanComplete.OnNext(new PlanCompleteEventArgs(0));

                    // wait until install is complete
                    firstEngine.WaitUntil(e => e.Apply(It.IsAny<IntPtr>()));

                    // now signal it's completed
                    firstApplyComplete.OnNext(new ApplyCompleteEventArgs(0, ApplyRestart.None));
                }

                using (IntegrationTestHelper.WithFakeInstallDirectory("SampleUpdatingApp.1.1.0.0.nupkg", out dir)) {
                    // install version 1.1 again
                    var secondKernel = new TinyIoCContainer();
                    var secondFactory = new Mock<IProcessFactory>();
                    secondKernel.Register(secondFactory.Object);

                    var secondRouter = new RoutingState();
                    var secondDetectPackage = new Subject<DetectPackageCompleteEventArgs>();
                    var secondPlanComplete = new Subject<PlanCompleteEventArgs>();
                    var secondApplyComplete = new Subject<ApplyCompleteEventArgs>();
                    var secondError = new Subject<ErrorEventArgs>();
                    var secondEngine = new Mock<IEngine>();

                    var secondEvents = new Mock<IWiXEvents>();
                    secondEvents.SetupGet(x => x.DetectPackageCompleteObs).Returns(secondDetectPackage);
                    secondEvents.SetupGet(x => x.ErrorObs).Returns(secondError);
                    secondEvents.SetupGet(x => x.PlanCompleteObs).Returns(secondPlanComplete);
                    secondEvents.SetupGet(x => x.ApplyCompleteObs).Returns(secondApplyComplete);
                    secondEvents.SetupGet(x => x.Engine).Returns(secondEngine.Object);

                    secondEvents.SetupGet(x => x.DisplayMode).Returns(Display.Full);
                    secondEvents.SetupGet(x => x.Action).Returns(LaunchAction.Install);

                    var secondFixture = new WixUiBootstrapper(secondEvents.Object, secondKernel, secondRouter, null, dir, targetRootDirectory);
                    RxApp.GetAllServices<ICreatesObservableForProperty>().Any().ShouldBeTrue();

                    // initialize the install process
                    secondDetectPackage.OnNext(new DetectPackageCompleteEventArgs("Foo", 0, PackageState.Absent));

                    // navigate to the next VM
                    var viewModel = secondRouter.GetCurrentViewModel() as WelcomeViewModel;
                    viewModel.ShouldProceed.Execute(null);

                    // signal to start the install
                    secondPlanComplete.OnNext(new PlanCompleteEventArgs(0));

                    // wait until install is complete
                    secondEngine.WaitUntil(e => e.Apply(It.IsAny<IntPtr>()));

                    // now signal it's completed
                    secondApplyComplete.OnNext(new ApplyCompleteEventArgs(0, ApplyRestart.None));

                    var folder = Path.Combine(targetRootDirectory, "SampleUpdatingApp", "app-1.1.0.0");
                    Assert.True(Directory.Exists(folder));

                    var exe = Path.Combine(folder, "SampleUpdatingApp.exe");

                    // we expect that it opens the main exe again
                    secondFactory.Verify(
                        p => p.Start(exe),
                        Times.Once(),
                        "We expect a process to be executed here, but it ain't...");
                }
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
    }
}