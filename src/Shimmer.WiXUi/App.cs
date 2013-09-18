using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using ReactiveUI;
using ReactiveUI.Routing;
using Shimmer.Client.WiXUi;
using Shimmer.WiXUi.ViewModels;
using Shimmer.WiXUi.Views;
using LogLevel = Microsoft.Tools.WindowsInstallerXml.Bootstrapper.LogLevel;

namespace Shimmer.WiXUi
{
    public class App : BootstrapperApplication, IWiXEvents, IEnableLogger
    {
        Application theApp;
        Dispatcher uiDispatcher;

        protected override void Run()
        {
            RxApp.LoggerFactory = _ => new FileLogger("Shimmer") { Level = ReactiveUI.LogLevel.Info };
            ReactiveUIMicro.RxApp.ConfigureFileLogging(); // HACK: we can do better than this later

            this.Log().Info("Bootstrapper started");

            theApp = new Application();

            // NB: These are mirrored instead of just exposing Command because
            // Command is impossible to mock, since there is no way to set any
            // of its properties
            DisplayMode = Command.Display;
            Action = Command.Action;

            this.Log().Info("WiX events: DisplayMode: {0}, Action: {1}", Command.Display, Command.Action);

#if DEBUG
            Debugger.Launch();
#endif
            setupWiXEventHooks();

            var bootstrapper = new WixUiBootstrapper(this);

            theApp.MainWindow = new RootWindow
            {
                viewHost = { Router = bootstrapper.Router }
            };

            MainWindowHwnd = IntPtr.Zero;
            if (Command.Display == Display.Full)
            {
                MainWindowHwnd = new WindowInteropHelper(theApp.MainWindow).Handle;
                uiDispatcher = theApp.MainWindow.Dispatcher;
                theApp.Run(theApp.MainWindow);
            }

            Engine.Quit(0);
        }

        public new IEngine Engine { get; protected set; }
        public IntPtr MainWindowHwnd { get; protected set; }

        public Display DisplayMode { get; protected set; }
        public LaunchAction Action { get; protected set; }

        public void ShouldQuit()
        {
            this.Log().Info("Bootstrapper finishing");

            // NB: For some reason, we can't get DispatcherScheduler.Current
            // here, WiX is doing something very strange post-apply
            uiDispatcher.Invoke(new Action(() =>
            {
                theApp.MainWindow.Close();
                theApp.Shutdown();
                Engine.Quit(0);
            }));
        }

        #region Extremely dull code to set up IWiXEvents
        void setupWiXEventHooks()
        {
            DetectBeginObs = Observable.FromEventPattern<DetectBeginEventArgs>(x => DetectBegin += x, x => DetectBegin -= x).Select(x => x.EventArgs);
            DetectPackageCompleteObs = Observable.FromEventPattern<DetectPackageCompleteEventArgs>(x => DetectPackageComplete += x, x => DetectPackageComplete -= x).Select(x => x.EventArgs);
            DetectRelatedBundleObs = Observable.FromEventPattern<DetectRelatedBundleEventArgs>(x => DetectRelatedBundle += x, x => DetectRelatedBundle -= x).Select(x => x.EventArgs);
            PlanPackageBeginObs = Observable.FromEventPattern<PlanPackageBeginEventArgs>(x => PlanPackageBegin += x, x => PlanPackageBegin -= x).Select(x => x.EventArgs);
            PlanCompleteObs = Observable.FromEventPattern<PlanCompleteEventArgs>(x => PlanComplete += x, x => PlanComplete -= x).Select(x => x.EventArgs);
            ApplyBeginObs = Observable.FromEventPattern<ApplyBeginEventArgs>(x => ApplyBegin += x, x => ApplyBegin -= x).Select(x => x.EventArgs);
            ApplyCompleteObs = Observable.FromEventPattern<ApplyCompleteEventArgs>(x => ApplyComplete += x, x => ApplyComplete -= x).Select(x => x.EventArgs);
            ResolveSourceObs = Observable.FromEventPattern<ResolveSourceEventArgs>(x => ResolveSource += x, x => ResolveSource -= x).Select(x => x.EventArgs);
            ErrorObs = Observable.FromEventPattern<ErrorEventArgs>(x => Error += x, x => Error -= x).Select(x => x.EventArgs);
            ExecuteMsiMessageObs = Observable.FromEventPattern<ExecuteMsiMessageEventArgs>(x => ExecuteMsiMessage += x, x => ExecuteMsiMessage -= x).Select(x => x.EventArgs);
            ExecuteProgressObs = Observable.FromEventPattern<ExecuteProgressEventArgs>(x => ExecuteProgress += x, x => ExecuteProgress -= x).Select(x => x.EventArgs);
            ProgressObs = Observable.FromEventPattern<ProgressEventArgs>(x => Progress += x, x => Progress -= x).Select(x => x.EventArgs);
            CacheAcquireBeginObs = Observable.FromEventPattern<CacheAcquireBeginEventArgs>(x => CacheAcquireBegin += x, x => CacheAcquireBegin -= x).Select(x => x.EventArgs);
            CacheCompleteObs = Observable.FromEventPattern<CacheCompleteEventArgs>(x => CacheComplete += x, x => CacheComplete -= x).Select(x => x.EventArgs);
            Engine = new EngineWrapper(((BootstrapperApplication)this).Engine);
        }

        public IObservable<DetectBeginEventArgs> DetectBeginObs { get; private set; }
        public IObservable<DetectPackageCompleteEventArgs> DetectPackageCompleteObs { get; private set; }
        public IObservable<DetectRelatedBundleEventArgs> DetectRelatedBundleObs { get; private set; }
        public IObservable<PlanPackageBeginEventArgs> PlanPackageBeginObs { get; private set; }
        public IObservable<PlanCompleteEventArgs> PlanCompleteObs { get; private set; }
        public IObservable<ApplyBeginEventArgs> ApplyBeginObs { get; private set; }
        public IObservable<ApplyCompleteEventArgs> ApplyCompleteObs { get; private set; }
        public IObservable<ResolveSourceEventArgs> ResolveSourceObs { get; private set; }
        public IObservable<ErrorEventArgs> ErrorObs { get; private set; }
        public IObservable<ExecuteMsiMessageEventArgs> ExecuteMsiMessageObs { get; private set; }
        public IObservable<ExecuteProgressEventArgs> ExecuteProgressObs { get; private set; }
        public IObservable<ProgressEventArgs> ProgressObs { get; private set; }
        public IObservable<CacheAcquireBeginEventArgs> CacheAcquireBeginObs { get; private set; }
        public IObservable<CacheCompleteEventArgs> CacheCompleteObs { get; private set; }
        #endregion
    }
}
