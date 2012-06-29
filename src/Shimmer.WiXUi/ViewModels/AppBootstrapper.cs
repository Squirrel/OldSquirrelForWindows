using System;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using ReactiveUI;
using ReactiveUI.Routing;
using TinyIoC;

namespace Shimmer.WiXUi.ViewModels
{
    public interface IWiXEvents
    {
        IObservable<DetectBeginEventArgs> DetectBeginObs { get; }
        IObservable<DetectPackageCompleteEventArgs> DetectPackageCompleteObs { get; } 
        IObservable<DetectRelatedBundleEventArgs> DetectRelatedBundleObs { get; }

        IObservable<PlanPackageBeginEventArgs> PlanPackageBeginObs { get; }
        IObservable<PlanCompleteEventArgs> PlanCompleteObs { get; }

        IObservable<ApplyBeginEventArgs> ApplyBeginObs { get; }
        IObservable<ApplyCompleteEventArgs> ApplyCompleteObs { get; }

        IObservable<ResolveSourceEventArgs> ResolveSourceObs { get; }
        IObservable<ErrorEventArgs> ErrorObs { get; }

        IObservable<ExecuteMsiMessageEventArgs> ExecuteMsiMessageObs { get; }
        IObservable<ExecuteProgressEventArgs> ExecuteProgressObs { get; }
        IObservable<ProgressEventArgs> ProgressObs { get; }
        IObservable<CacheAcquireBeginEventArgs> CacheAcquireBeginObs { get; }
        IObservable<CacheCompleteEventArgs> CacheCompleteObs { get; }
    }

    public interface IAppBootstrapper : IScreen { }

    public class AppBootstrapper : ReactiveObject, IAppBootstrapper
    {
        public IRoutingState Router { get; protected set; }
        public static TinyIoCContainer Kernel { get; protected set; }

        public AppBootstrapper(IWiXEvents wixEvents, TinyIoCContainer testKernel = null, IRoutingState router = null)
        {
            Kernel = testKernel ?? createDefaultKernel();
            Kernel.Register<IAppBootstrapper>(this).AsSingleton();
            Kernel.Register<IScreen>(this);
            Kernel.Register(wixEvents);

            Router = router ?? new RoutingState();

            RxApp.ConfigureServiceLocator(
                (type, contract) => Kernel.Resolve(type, contract),
                (type, contract) => Kernel.ResolveAll(type));
       }

        TinyIoCContainer createDefaultKernel()
        {
            var ret = new TinyIoCContainer();
            return ret;
        }
    }
}