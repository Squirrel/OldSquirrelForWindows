using System;
using NuGet;
using ReactiveUIMicro.Xaml;
using System.ComponentModel;

namespace Squirrel.Client.WiXUi
{
    public interface IWelcomeViewModel : INotifyPropertyChanged
    {
        IPackage PackageMetadata { get; set; }
        string Title { get; }
        string Description { get; }
        ReactiveCommand ShouldProceed { get; }
    }

    public interface IInstallingViewModel : INotifyPropertyChanged
    {
        IPackage PackageMetadata { get; set; }
        IObserver<int> ProgressValue { get; }
        int LatestProgress { get; }
    }

    public interface IUninstallingViewModel : INotifyPropertyChanged
    {
        IPackage PackageMetadata { get; set; }
        IObserver<int> ProgressValue { get; }
        int LatestProgress { get; }
    }

    public interface IErrorViewModel : INotifyPropertyChanged
    {
        IPackage PackageMetadata { get; set; }
        UserError Error { get; set; }
        ReactiveCommand Shutdown { get; }
        ReactiveCommand OpenLogsFolder { get; }
    }
}
