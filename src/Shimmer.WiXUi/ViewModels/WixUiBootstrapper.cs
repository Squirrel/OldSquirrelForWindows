using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Windows;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using NuGet;
using ReactiveUI;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using Shimmer.Client;
using Shimmer.Client.WiXUi;
using Shimmer.Core;
using TinyIoC;

namespace Shimmer.WiXUi.ViewModels
{
    public class WixUiBootstrapper : ReactiveObject, IWixUiBootstrapper
    {
        public IRoutingState Router { get; protected set; }
        public IWiXEvents WiXEvents { get; protected set; }
        public static TinyIoCContainer Kernel { get; protected set; }

        readonly Lazy<ReleaseEntry> _BundledRelease;
        public ReleaseEntry BundledRelease { get { return _BundledRelease.Value; } }

        public WixUiBootstrapper(IWiXEvents wixEvents, TinyIoCContainer testKernel = null, IRoutingState router = null)
        {
            Kernel = testKernel ?? createDefaultKernel();
            Kernel.Register<IWixUiBootstrapper>(this).AsSingleton();
            Kernel.Register<IScreen>(this);
            Kernel.Register(wixEvents);

            Router = router ?? new RoutingState();
            WiXEvents = wixEvents;

            _BundledRelease = new Lazy<ReleaseEntry>(readBundledReleasesFile);

            registerExtensionDlls(Kernel);

            RxApp.ConfigureServiceLocator(
                (type, contract) => Kernel.Resolve(type, contract),
                (type, contract) => Kernel.ResolveAll(type));

            UserError.RegisterHandler(ex => {
                var errorVm = RxApp.GetService<IErrorViewModel>();
                errorVm.Error = ex;
                Router.Navigate.Execute(errorVm);

                return Observable.Return(RecoveryOptionResult.CancelOperation);
            });

            var bundledPackageMetadata = openBundledPackage();

            wixEvents.DetectPackageCompleteObs.Subscribe(eventArgs => {
                var error = convertHResultToError(eventArgs.Status);
                if (error != null) {
                    UserError.Throw(error);
                    return;
                }

                // TODO: If the app is already installed, run it and bail

                if (wixEvents.Command.Action == LaunchAction.Uninstall) {
                    var updateManager = new UpdateManager("http://lol", BundledRelease.PackageName, FrameworkVersion.Net40);

                    var updateLock = updateManager.AcquireUpdateLock();
                    updateManager.FullUninstall()
                        .ObserveOn(RxApp.DeferredScheduler)
                        .Log(this, "Failed uninstall")
                        .Finally(updateLock.Dispose)
                        .Subscribe(
                            _ => wixEvents.Engine.Plan(LaunchAction.Uninstall),
                            ex => UserError.Throw("Failed to uninstall application", ex),
                            () => wixEvents.ShouldQuit());

                    return;
                }

                if (wixEvents.Command.Action == LaunchAction.Install) {
                    if (wixEvents.Command.Display != Display.Full) {
                        wixEvents.Engine.Plan(LaunchAction.Install);
                        return;
                    }

                    var welcomeVm = RxApp.GetService<IWelcomeViewModel>();
                    welcomeVm.PackageMetadata = bundledPackageMetadata;
                    welcomeVm.ShouldProceed.Subscribe(_ => wixEvents.Engine.Plan(LaunchAction.Install));

                    Router.Navigate.Execute(welcomeVm);
                }
            });

            wixEvents.PlanCompleteObs.Subscribe(eventArgs => {
                var error = convertHResultToError(eventArgs.Status);
                if (error != null) {
                    UserError.Throw(error);
                    return;
                }

                if (wixEvents.Command.Action != LaunchAction.Install) {
                    wixEvents.Engine.Apply(wixEvents.MainWindowHwnd);
                    return;
                }

                // NB: Create a dummy subject to receive progress if we're in silent mode
                // TODO: Progress, it does nothing!
                IObserver<int> progress = new Subject<int>();
                if (wixEvents.Command.Display == Display.Full) {
                    var installingVm = RxApp.GetService<IInstallingViewModel>();
                    progress = installingVm.ProgressValue;
                    installingVm.PackageMetadata = bundledPackageMetadata;
                    Router.Navigate.Execute(installingVm);
                }

                // NB: This bit of code is a bit clever. The binaries that WiX 
                // has installed *itself* meets the qualifications for being a
                // Shimmer update directory (a RELEASES file and the corresponding 
                // NuGet packages). 
                //
                // So, in order to reuse some code and not write the same things 
                // twice we're going to "Eigenupdate" from our own directory; 
                // UpdateManager will operate in bootstrap mode and create a 
                // local directory for us. 
                //
                // Then, we create a *new* UpdateManager whose target is the normal 
                // update URL - we can then apply delta updates against the bundled 
                // NuGet package to get up to vCurrent. The reason we go through
                // this rigamarole is so that developers don't have to rebuild the 
                // installer as often (never, technically).

                var fxVersion = determineFxVersionFromPackage(bundledPackageMetadata);
                var eigenUpdater = new UpdateManager(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), BundledRelease.PackageName, fxVersion);

                var eigenLock = eigenUpdater.AcquireUpdateLock();

                eigenUpdater.CheckForUpdate()
                    .SelectMany(x => eigenUpdater.DownloadReleases(x.ReleasesToApply))
                    .Finally(eigenLock.Dispose)
                    .SelectMany(x => {
                        var realUpdateManager = new UpdateManager(bundledPackageMetadata.ProjectUrl.ToString(), BundledRelease.PackageName, fxVersion);

                        return realUpdateManager.UpdateApp()
                            .Select(_ => Unit.Default)
                            .LoggedCatch(this, Observable.Return(Unit.Default), "Failed to update to latest remote version");
                    })
                    .ObserveOn(RxApp.DeferredScheduler)
                    .Subscribe(
                        _ => wixEvents.Engine.Apply(wixEvents.MainWindowHwnd),
                        ex => UserError.Throw("Failed to install application", ex));
            });

            wixEvents.ApplyCompleteObs.Subscribe(eventArgs => {
                var error = convertHResultToError(eventArgs.Status);
                if (error != null) {
                    UserError.Throw(error);
                    return;
                }

                if (wixEvents.Command.Display != Display.Full) {
                    wixEvents.ShouldQuit();
                }

                // TODO: Figure out what the "main app" is and run it
            });

            wixEvents.ErrorObs.Subscribe(eventArgs => UserError.Throw("An installation error has occurred: " + eventArgs.ErrorMessage));
        }

        UserError convertHResultToError(int status)
        {
            // NB: WiX passes this as an int which makes it impossible for us to
            // grok properly
            var hr = BitConverter.ToUInt32(BitConverter.GetBytes(status), 0);
            if ((hr & 0x80000000) == 0) {
                return null;
            }

            return new UserError(String.Format("An installer error has occurred: 0x{0:x}", hr));
        }

        IPackage openBundledPackage()
        {
            var fi = new FileInfo(Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), 
                BundledRelease.Filename));

            return new ZipPackage(fi.FullName);
        }

        static FrameworkVersion determineFxVersionFromPackage(IPackage package)
        {
            return package.GetFiles().Any(x => x.Path.Contains("lib") && x.Path.Contains("45"))
                ? FrameworkVersion.Net45
                : FrameworkVersion.Net40;
        }

        ReleaseEntry readBundledReleasesFile()
        {
            var release = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RELEASES"));
            if (!release.Exists) {
                UserError.Throw("This installer is incorrectly configured, please contact the author", 
                    new FileNotFoundException(release.FullName));
                return null;
            }

            ReleaseEntry ret;

            try {
                ret = ReleaseEntry.ParseReleaseFile(File.ReadAllText(release.FullName, Encoding.UTF8)).Single();
            } catch (Exception ex) {
                this.Log().ErrorException("Couldn't read bundled RELEASES file", ex);
                UserError.Throw("This installer is incorrectly configured, please contact the author", ex);
                return null;
            }

            return ret;
        }

        void registerExtensionDlls(TinyIoCContainer kernel)
        {
            var di = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            var extensions = di.GetFiles("*.dll")
                .Where(x => x.FullName != Assembly.GetExecutingAssembly().Location)
                .SelectMany(x => {
                    try {
                        return new[] {Assembly.LoadFile(x.FullName)};
                    } catch (Exception ex) {
                        this.Log().WarnException("Couldn't load " + x.Name, ex);
                        return Enumerable.Empty<Assembly>();
                    }
                })
                .SelectMany(x => x.GetModules()).SelectMany(x => x.GetTypes())
                .Where(x => typeof(IWiXCustomUi).IsAssignableFrom(x) && !x.IsAbstract)
                .SelectMany(x => {
                    try {
                        return new[] {(IWiXCustomUi) Activator.CreateInstance(x)};
                    } catch (Exception ex) {
                        this.Log().WarnException("Couldn't create instance: " + x.FullName, ex);
                        return Enumerable.Empty<IWiXCustomUi>();
                    }
                });

            foreach (var extension in extensions) {
                extension.RegisterTypes(kernel);
            }
        }

        TinyIoCContainer createDefaultKernel()
        {
            var ret = new TinyIoCContainer();
            return ret;
        }
    }
}