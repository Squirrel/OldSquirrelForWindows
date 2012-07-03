using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using ReactiveUI.Routing;
using Shimmer.Client;
using Shimmer.Core;
using Shimmer.WiXUi.ViewModels;
using Shimmer.WiXUi.Views;

namespace Shimmer.WiXUi
{
    public class App : BootstrapperApplication, IWiXEvents
    {
        protected override void Run()
        {
            var app = new Application();

            setupWiXEventHooks();

            var bootstrapper = new WixUiBootstrapper(this);

            app.MainWindow = new RootWindow {
                // XXX: Fix this casting shit in ReactiveUI.Routing
                viewHost = {Router = (RoutingState) bootstrapper.Router}
            };
        }

        public new IEngine Engine { get; protected set; }

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
            Engine = new EngineWrapper(((BootstrapperApplication) this).Engine);
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
