using System;
using NuGet;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;

namespace Squirrel.Client.WiXUi
{
    public interface IWelcomeViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        string Title { get; }
        string Description { get; }
        ReactiveCommand ShouldProceed { get; }
    }

    public interface IInstallingViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        IObserver<int> ProgressValue { get; }
        int LatestProgress { get; }
    }

    public interface IUninstallingViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        IObserver<int> ProgressValue { get; }
        int LatestProgress { get; }
    }

    public interface IErrorViewModel : IRoutableViewModel
    {
        IPackage PackageMetadata { get; set; }
        UserError Error { get; set; }
        ReactiveCommand Shutdown { get; }
        ReactiveCommand OpenLogsFolder { get; }
    }
}
