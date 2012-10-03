using System;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;

namespace Shimmer.Client.WiXUi
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

        IEngine Engine { get; }
        IntPtr MainWindowHwnd { get; }
        Display DisplayMode { get; }
        LaunchAction Action { get; }

        void ShouldQuit();
    }
}